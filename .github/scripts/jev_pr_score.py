#!/usr/bin/env python3
"""Score a pull request with the Jev decision model (Venice Decisions API).

Fetches the pull request's title, description, changed files and diff through
the GitHub API, then asks Jev how likely the pull request is a good addition to
the repository. The score (0-100) is exposed to GitHub Actions through
GITHUB_OUTPUT and a ready-to-post pull request comment is written to the
comment file.

Environment:
    PR_NUMBER       pull request number
    REPO            repository in owner/name form
    GH_TOKEN        GitHub token (use github.token in workflows)
    VENICE_API_KEY  Venice API key used to call the Decisions API
    GITHUB_OUTPUT   set by GitHub Actions (optional)

Outputs (written to GITHUB_OUTPUT):
    score            integer 0-100
    only_projects_md true|false
    can_automerge    true|false (only Projects.md modified and score >= 70)

Usage:
    jev_pr_score.py <comment-outfile>
"""

from __future__ import annotations

import json
import os
import sys
import urllib.error
import urllib.request

GITHUB_API = "https://api.github.com"
VENICE_DECISIONS_URL = "https://api.venice.ai/api/v1/decisions"
JEV_MODEL = "jev-latest"
SCORE_THRESHOLD = 70
MARKER = "<!-- jev-pr-score -->"
MAX_DESCRIPTION_CHARS = 4000
MAX_DIFF_CHARS = 60000
TARGET_FILE = "Projects.md"


def env(name: str) -> str:
    value = os.environ.get(name, "")
    if not value:
        raise SystemExit(f"missing required environment variable: {name}")
    return value


def github_get(path: str, accept: str | None = None) -> str:
    headers = {
        "Authorization": f"Bearer {os.environ['GH_TOKEN']}",
        "User-Agent": "x402dev-jev-pr-score",
        "Accept": accept or "application/vnd.github+json",
    }
    request = urllib.request.Request(GITHUB_API + path, headers=headers)
    with urllib.request.urlopen(request, timeout=60) as response:
        return response.read().decode("utf-8", errors="replace")


def fetch_pull_request(repo: str, number: str) -> dict:
    data = json.loads(github_get(f"/repos/{repo}/pulls/{number}"))
    return {
        "title": data.get("title") or "",
        "body": data.get("body") or "",
        "url": data.get("html_url") or f"https://github.com/{repo}/pull/{number}",
    }


def fetch_changed_files(repo: str, number: str) -> list[str]:
    filenames: list[str] = []
    page = 1
    while True:
        data = json.loads(
            github_get(f"/repos/{repo}/pulls/{number}/files?per_page=100&page={page}")
        )
        filenames.extend(item["filename"] for item in data)
        if len(data) < 100:
            break
        page += 1
    return filenames


def fetch_diff(repo: str, number: str) -> str:
    return github_get(
        f"/repos/{repo}/pulls/{number}", accept="application/vnd.github.v3.diff"
    )


def projects_md_has_additions(diff: str) -> bool:
    """True when the diff adds at least one line to Projects.md."""
    in_target = False
    for line in diff.splitlines():
        if line.startswith("diff --git "):
            in_target = False
        elif line.startswith("+++ "):
            path = line[4:].strip()
            if path.startswith("b/"):
                path = path[2:]
            in_target = path == TARGET_FILE
        elif in_target and line.startswith("+") and not line.startswith("+++"):
            return True
    return False


def score_with_jev(state: str) -> tuple[int, float]:
    payload = {
        "model": JEV_MODEL,
        "state": state,
        "questions": {
            "pr_score": {
                "type": "score",
                "instructions": (
                    "Score how likely this pull request is a good addition to the "
                    "x402dev repository. A good pull request follows the project's "
                    "conventions, provides real value, does not add broken, disallowed "
                    "or harmful content, and does not break existing functionality. "
                    "A pull request that only adds a valid, well-formed endpoint to "
                    "Projects.md is routine and expected. "
                    "Choose the level that best matches the pull request."
                ),
                "criteria": [
                    "0/10 - Very poor addition: broken, malicious, spam, or harmful",
                    "1/10 - Very poor addition with almost no value",
                    "2/10 - Poor addition with several serious problems",
                    "3/10 - Poor addition with some value but real problems",
                    "4/10 - Below average with meaningful issues",
                    "5/10 - Average: acceptable but not compelling",
                    "6/10 - Decent addition with minor concerns",
                    "7/10 - Good addition that fits the repository well",
                    "8/10 - Very good addition, clearly beneficial",
                    "9/10 - Excellent addition, high value",
                    "10/10 - Outstanding addition, exactly what the repository needs",
                ],
            }
        },
    }
    body = json.dumps(payload).encode("utf-8")
    request = urllib.request.Request(
        VENICE_DECISIONS_URL,
        data=body,
        method="POST",
        headers={
            "Authorization": f"Bearer {os.environ['VENICE_API_KEY']}",
            "Content-Type": "application/json",
            "Accept": "application/json",
        },
    )
    with urllib.request.urlopen(request, timeout=120) as response:
        data = json.loads(response.read().decode("utf-8"))

    answer = data["answers"]["pr_score"]
    raw_score = float(answer["score"])
    confidence = float(answer.get("confidence") or 0.0)
    percentage = int(round(max(0.0, min(10.0, raw_score)) * 10))
    return percentage, confidence


def build_state(pr: dict, files: list[str], diff: str) -> str:
    sections = [
        f"Pull request title: {pr['title']}",
        "",
        "Pull request description:",
        pr["body"][:MAX_DESCRIPTION_CHARS],
        "",
        "Changed files:",
    ]
    sections.extend(f"- {filename}" for filename in files)
    sections.append("")
    sections.append("Diff:")
    sections.append(diff[:MAX_DIFF_CHARS])
    return "\n".join(sections)


def build_comment(
    score: int, confidence: float, only_projects_md: bool, can_automerge: bool
) -> str:
    lines = [
        MARKER,
        "## Jev pull request score",
        "",
        f"**{score}/100** - how likely this pull request is a good addition to the "
        f"repository (confidence {confidence * 100:.0f}%).",
        "",
    ]
    if can_automerge:
        lines.append(
            "This pull request only modifies `Projects.md` and scores above the "
            "70-point auto-merge threshold, so it is eligible for automatic merging."
        )
    elif only_projects_md:
        lines.append(
            "This pull request only modifies `Projects.md` but did not score above "
            "the 70-point auto-merge threshold, so it is **not** eligible for "
            "automatic merging."
        )
    else:
        lines.append(
            "This pull request changes more than `Projects.md`, so it is not "
            "eligible for automatic merging."
        )
    return "\n".join(lines)


def write_outputs(score: int, only_projects_md: bool, can_automerge: bool) -> None:
    output_file = os.environ.get("GITHUB_OUTPUT")
    if not output_file:
        return
    with open(output_file, "a", encoding="utf-8") as handle:
        handle.write(f"score={score}\n")
        handle.write(f"only_projects_md={'true' if only_projects_md else 'false'}\n")
        handle.write(f"can_automerge={'true' if can_automerge else 'false'}\n")


def main(argv: list[str]) -> int:
    if len(argv) != 2:
        print(f"usage: {argv[0]} <comment-outfile>", file=sys.stderr)
        return 2

    repo = env("REPO")
    number = env("PR_NUMBER")

    pr = fetch_pull_request(repo, number)
    files = fetch_changed_files(repo, number)
    diff = fetch_diff(repo, number)

    state = build_state(pr, files, diff)
    try:
        score, confidence = score_with_jev(state)
    except (urllib.error.HTTPError, urllib.error.URLError, KeyError, ValueError) as exc:
        print(f"Jev scoring failed: {exc}", file=sys.stderr)
        return 1

    only_projects_md = files == [TARGET_FILE] and projects_md_has_additions(diff)
    can_automerge = only_projects_md and score >= SCORE_THRESHOLD

    with open(argv[1], "w", encoding="utf-8") as handle:
        handle.write(build_comment(score, confidence, only_projects_md, can_automerge))

    write_outputs(score, only_projects_md, can_automerge)

    print(
        f"Jev score for pull request #{number}: {score}/100 "
        f"(confidence {confidence * 100:.0f}%)"
    )
    print(f"only_projects_md={only_projects_md} can_automerge={can_automerge}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))

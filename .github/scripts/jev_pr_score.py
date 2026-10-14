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
    subdomain_penalty integer, points deducted from the score when the main
                      new URL added to Projects.md is hosted on a subdomain

The final score is capped lower (SUBJECT_SUBDOMAIN_PENALTY points) when the
primary URL of an added Projects.md entry is a subdomain (e.g. api.example.com
instead of example.com), so subdomain-hosted endpoints are unlikely to reach
the auto-merge threshold.

Usage:
    jev_pr_score.py <comment-outfile>
"""

from __future__ import annotations

import json
import os
import re
import sys
import urllib.error
import urllib.request
from urllib.parse import urlsplit

GITHUB_API = "https://api.github.com"
VENICE_DECISIONS_URL = "https://api.venice.ai/api/v1/decisions"
JEV_MODEL = "jev-latest"
SCORE_THRESHOLD = 70
MARKER = "<!-- jev-pr-score -->"
MAX_DESCRIPTION_CHARS = 4000
MAX_DIFF_CHARS = 60000
TARGET_FILE = "Projects.md"
# Deterministic points deducted from the Jev score when the main new URL is a
# subdomain (e.g. api.example.com instead of example.com). Subdomain-hosted
# endpoints are more likely to be ephemeral, free-tier or non-production
# infrastructure, so the final score is capped lower regardless of Jev's rating.
SUBJECT_SUBDOMAIN_PENALTY = 15
# Two-part public suffixes: example.co.uk is a root domain, not a subdomain.
TWO_PART_SUFFIXES = {
    "ac.uk", "co.uk", "gov.uk", "me.uk", "net.uk", "org.uk",
    "com.au", "net.au", "org.au",
    "com.br", "net.br", "org.br",
    "co.in", "net.in", "org.in",
    "co.jp", "ne.jp", "or.jp",
    "com.mx", "org.mx",
    "com.cn", "net.cn", "org.cn",
    "co.nz", "net.nz", "org.nz",
    "co.za", "org.za",
}
URL_REGEX = re.compile(r"https?://[^\s)<>\"'`]+", re.IGNORECASE)


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


def added_projects_md_lines(diff: str) -> list[str]:
    """Return the content of every line added to Projects.md in the diff."""
    lines: list[str] = []
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
            lines.append(line[1:])
    return lines


def projects_md_has_additions(diff: str) -> bool:
    """True when the diff adds at least one line to Projects.md."""
    return bool(added_projects_md_lines(diff))


def trim_url(url: str) -> str:
    """Strip trailing markdown/punctuation noise from a matched URL."""
    return url.rstrip(".,;:!?*_~\"'`")


def main_urls_of_lines(added_lines: list[str]) -> list[str]:
    """Return the primary (first) URL of each added Projects.md entry."""
    urls: list[str] = []
    for line in added_lines:
        match = URL_REGEX.search(line)
        if match:
            urls.append(trim_url(match.group(0)))
    return urls


def is_subdomain_host(host: str) -> bool:
    """True when the host is a subdomain of a registrable domain (e.g. api.example.com)."""
    host = host.lower().rstrip(".")
    if host.startswith("www."):
        host = host[4:]
    labels = host.split(".")
    if len(labels) < 2:
        return False
    # A two-part public suffix (e.g. .co.uk) makes the registrable domain 3 labels.
    if labels[-2] + "." + labels[-1] in TWO_PART_SUFFIXES:
        return len(labels) > 3
    return len(labels) > 2


def has_subdomain_main_url(urls: list[str]) -> bool:
    """True when any primary URL's host is a subdomain."""
    for url in urls:
        try:
            host = urlsplit(url).hostname
        except ValueError:
            continue
        if host and is_subdomain_host(host):
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
                    "A pull request whose main new URL is hosted on a subdomain "
                    "(e.g. api.example.com rather than example.com) is lower quality "
                    "and should be scored lower. "
                    "Choose the level that best matches the pull request."
                ),
                "criteria": [
                    "Very poor addition: broken, malicious, spam, or harmful",
                    "Very poor addition with almost no value",
                    "Poor addition with several serious problems",
                    "Poor addition with some value but real problems",
                    "Below average with meaningful issues",
                    "Average: acceptable but not compelling",
                    "Decent addition with minor concerns",
                    "Good addition that fits the repository well",
                    "Very good addition, clearly beneficial",
                    "Excellent addition, high value",
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
    # The score is a probability-weighted level index over the criteria list
    # (0 = lowest level, len(criteria)-1 = highest). Normalize to 0-100.
    max_index = len(payload["questions"]["pr_score"]["criteria"]) - 1
    percentage = int(round(max(0.0, min(float(max_index), raw_score)) / max_index * 100))
    return percentage, confidence


def build_state(pr: dict, files: list[str], diff: str, main_url_subdomain: bool) -> str:
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
    if main_url_subdomain:
        sections.append(
            "NOTE: the main new URL added to Projects.md is hosted on a subdomain; "
            "this is a negative quality signal and should be scored lower."
        )
        sections.append("")
    sections.append("Diff:")
    sections.append(diff[:MAX_DIFF_CHARS])
    return "\n".join(sections)


def build_comment(
    score: int,
    confidence: float,
    only_projects_md: bool,
    can_automerge: bool,
    subdomain_penalty: int,
) -> str:
    lines = [
        MARKER,
        "## Jev pull request score",
        "",
        f"**{score}/100** - how likely this pull request is a good addition to the "
        f"repository (confidence {confidence * 100:.0f}%).",
        "",
    ]
    if subdomain_penalty:
        lines.append(
            f"The score was reduced by {subdomain_penalty} points because the main "
            "new URL is hosted on a subdomain."
        )
        lines.append("")
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


def write_outputs(
    score: int, only_projects_md: bool, can_automerge: bool, subdomain_penalty: int
) -> None:
    output_file = os.environ.get("GITHUB_OUTPUT")
    if not output_file:
        return
    with open(output_file, "a", encoding="utf-8") as handle:
        handle.write(f"score={score}\n")
        handle.write(f"only_projects_md={'true' if only_projects_md else 'false'}\n")
        handle.write(f"can_automerge={'true' if can_automerge else 'false'}\n")
        handle.write(f"subdomain_penalty={subdomain_penalty}\n")


def main(argv: list[str]) -> int:
    if len(argv) != 2:
        print(f"usage: {argv[0]} <comment-outfile>", file=sys.stderr)
        return 2

    repo = env("REPO")
    number = env("PR_NUMBER")

    pr = fetch_pull_request(repo, number)
    files = fetch_changed_files(repo, number)
    diff = fetch_diff(repo, number)

    added_lines = added_projects_md_lines(diff)
    main_url_subdomain = has_subdomain_main_url(main_urls_of_lines(added_lines))

    state = build_state(pr, files, diff, main_url_subdomain)
    try:
        score, confidence = score_with_jev(state)
    except (urllib.error.HTTPError, urllib.error.URLError, KeyError, ValueError) as exc:
        print(f"Jev scoring failed: {exc}", file=sys.stderr)
        return 1

    # Deterministically ensure a subdomain main URL scores lower.
    subdomain_penalty = SUBJECT_SUBDOMAIN_PENALTY if main_url_subdomain else 0
    if subdomain_penalty:
        score = max(0, score - subdomain_penalty)

    only_projects_md = files == [TARGET_FILE] and bool(added_lines)
    can_automerge = only_projects_md and score >= SCORE_THRESHOLD

    with open(argv[1], "w", encoding="utf-8") as handle:
        handle.write(
            build_comment(score, confidence, only_projects_md, can_automerge, subdomain_penalty)
        )

    write_outputs(score, only_projects_md, can_automerge, subdomain_penalty)

    print(
        f"Jev score for pull request #{number}: {score}/100 "
        f"(confidence {confidence * 100:.0f}%)"
    )
    print(f"only_projects_md={only_projects_md} can_automerge={can_automerge}")
    if subdomain_penalty:
        print(f"subdomain_penalty={subdomain_penalty}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))

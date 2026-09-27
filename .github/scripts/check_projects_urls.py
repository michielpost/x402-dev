#!/usr/bin/env python3
"""Reject pull requests that add disallowed URLs to Projects.md.

The rules mirror the server-side validation in
src/x402dev.Services/X402ApiService.cs (AddX402ApiAsync):

  * hosts that are a literal IP address are not allowed
  * hosts containing a dashed IP pattern (e.g. 204-168-208-32) are not allowed
  * hosts containing a blocked domain substring (netlify, trycloudflare, workers.dev,
    sslip.io) are not allowed

Usage:
    check_projects_urls.py <diff-file> <comment-outfile>

Reads a unified diff (as produced by `gh pr diff`), inspects the URLs that were
*added* to Projects.md and writes a ready-to-post pull request comment to
<comment-outfile>. The result is also exposed to GitHub Actions through the
GITHUB_OUTPUT file as `violations=true|false`.
"""

from __future__ import annotations

import ipaddress
import os
import re
import sys
from urllib.parse import urlsplit

TARGET_FILE = "Projects.md"
BLOCKED_DOMAIN_SUBSTRINGS = ("netlify", "trycloudflare", "workers.dev", "sslip.io", "ts.net")
DASHED_IP_REGEX = re.compile(r"\d{1,3}-\d{1,3}-\d{1,3}-\d{1,3}")
URL_REGEX = re.compile(r"https?://[^\s)<>\"'`]+", re.IGNORECASE)
MARKER = "<!-- projects-md-url-check -->"


def added_lines(diff_text: str, target_file: str) -> list[str]:
    """Return the content of every line added to `target_file`."""
    lines: list[str] = []
    in_target = False

    for line in diff_text.splitlines():
        if line.startswith("diff --git "):
            in_target = False
        elif line.startswith("+++ "):
            path = line[4:].strip()
            if path.startswith("b/"):
                path = path[2:]
            in_target = path == target_file
        elif in_target and line.startswith("+") and not line.startswith("+++"):
            lines.append(line[1:])

    return lines


def trim_url(url: str) -> str:
    """Strip trailing markdown/punctuation noise from a matched URL."""
    return url.rstrip(".,;:!?*_~\"'`")


def is_ip_host(host: str) -> bool:
    try:
        ipaddress.ip_address(host)
        return True
    except ValueError:
        return False


def check_url(url: str) -> str | None:
    """Return a violation reason for the URL, or None when it is allowed."""
    try:
        parts = urlsplit(url)
    except ValueError:
        return None

    host = parts.hostname
    if not host:
        return None

    if is_ip_host(host):
        return f"the host `{host}` is an IP address"

    if DASHED_IP_REGEX.search(host):
        return f"the host `{host}` contains an IP-like pattern (e.g. 204-168-208-32)"

    blocked = next((b for b in BLOCKED_DOMAIN_SUBSTRINGS if b in host.lower()), None)
    if blocked is not None:
        return f"the host `{host}` contains the blocked domain `{blocked}`"

    return None


def find_violations(diff_text: str) -> dict[str, str]:
    """Map each offending added URL to the reason it is not allowed."""
    violations: dict[str, str] = {}

    for line in added_lines(diff_text, TARGET_FILE):
        for match in URL_REGEX.findall(line):
            url = trim_url(match)
            reason = check_url(url)
            if reason is not None and url not in violations:
                violations[url] = reason

    return violations


def build_comment(violations: dict[str, str]) -> str:
    blocked_list = ", ".join(f"`{b}`" for b in BLOCKED_DOMAIN_SUBSTRINGS)
    lines = [
        MARKER,
        "## Projects.md URL check failed",
        "",
        "This pull request adds URLs to `Projects.md` that are not allowed. "
        "The same rules are enforced by the server when an endpoint is registered:",
        "",
        "- URLs whose host is an IP address are not allowed.",
        "- URLs whose host contains an IP-like pattern (e.g. `204-168-208-32`) are not allowed.",
        f"- URLs whose host contains a blocked domain are not allowed ({blocked_list}).",
        "",
        "Rejected URLs:",
        "",
    ]

    for url, reason in violations.items():
        lines.append(f"- `{url}` - {reason}")

    lines += [
        "",
        "Please update `Projects.md` to only use allowed, publicly resolvable domains. "
        "This pull request is being closed automatically.",
        "",
    ]

    return "\n".join(lines)


def write_outputs(violations: dict[str, str]) -> None:
    output_file = os.environ.get("GITHUB_OUTPUT")
    if not output_file:
        return

    with open(output_file, "a", encoding="utf-8") as handle:
        handle.write(f"violations={'true' if violations else 'false'}\n")
        handle.write(f"count={len(violations)}\n")


def main(argv: list[str]) -> int:
    if len(argv) != 3:
        print(f"usage: {argv[0]} <diff-file> <comment-outfile>", file=sys.stderr)
        return 2

    diff_path, comment_path = argv[1], argv[2]

    with open(diff_path, encoding="utf-8", errors="replace") as handle:
        diff_text = handle.read()

    violations = find_violations(diff_text)

    with open(comment_path, "w", encoding="utf-8") as handle:
        if violations:
            handle.write(build_comment(violations))

    write_outputs(violations)

    if violations:
        print(f"Found {len(violations)} disallowed URL(s):")
        for url, reason in violations.items():
            print(f"  {url} - {reason}")
    else:
        print("No disallowed URLs found in the added Projects.md lines.")

    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))

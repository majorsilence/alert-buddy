#!/usr/bin/env bash
# Fails if a file that is committed, or would be, matches the public-repo deny-list (PLAN.md section 14):
# signing material and ntfy access tokens must never reach this repository.
set -euo pipefail
cd "$(dirname "$0")/.."

fail=0

# Tracked files plus untracked-but-not-ignored ones, so it also guards the working tree before a commit.
files=$(git ls-files --cached --others --exclude-standard)

bad_names=$(printf '%s\n' "$files" | grep -E -i '\.(jks|keystore|p12|pfx)$' || true)
if [ -n "$bad_names" ]; then
  echo "hygiene: signing material must not be committed:" >&2
  printf '  %s\n' $bad_names >&2
  fail=1
fi

# An ntfy access token is "tk_" followed by 29 characters. Only text files are scanned.
while IFS= read -r f; do
  [ -f "$f" ] || continue
  if grep -I -n -E 'tk_[A-Za-z0-9]{29}' "$f" >/dev/null 2>&1; then
    echo "hygiene: $f contains something that looks like an ntfy access token" >&2
    fail=1
  fi
done <<< "$files"

if [ "$fail" -ne 0 ]; then
  exit 1
fi
echo "hygiene: ok"

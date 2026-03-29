#!/usr/bin/env bash
set -euo pipefail

range="${1:-}"
if [[ -z "$range" ]]; then
  echo "usage: $0 <git-range>" >&2
  exit 2
fi

status=0

while IFS= read -r commit; do
  [[ -z "$commit" ]] && continue

  body="$(git log --format=%B -n 1 "$commit")"
  if ! grep -qi 'out of scope' <<<"$body"; then
    continue
  fi

  if grep -Eq '^Tracking: (#[0-9]+|https?://.+)$' <<<"$body"; then
    continue
  fi

  echo "semport_guard: commit $commit contains 'out of scope' without a Tracking: reference" >&2
  status=1
done < <(git rev-list "$range")

exit "$status"

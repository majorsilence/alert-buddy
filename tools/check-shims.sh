#!/usr/bin/env bash
# Fails if a TEMP-SHIM in the source names a framework item that docs/framework-versions.md records as released.
# A released item has to be adopted and the shim deleted in the same commit (PLAN.md section 11.1, policy 2).
#
# docs/framework-versions.md carries one line, "Released items: F1 F3", listing register items the framework has released.
set -euo pipefail
cd "$(dirname "$0")/.."

released=$(grep -E '^Released items:' docs/framework-versions.md | sed -E 's/^Released items:[[:space:]]*//' | tr -d '.' | tr ',' ' ')
fail=0

# Every shim is expected to name an item: TEMP-SHIM (F8) or TEMP-SHIM (F10, F11).
while IFS= read -r hit; do
  file=${hit%%:*}
  for item in $(printf '%s' "$hit" | grep -o -E 'F[0-9]+'); do
    for r in $released; do
      if [ "$item" = "$r" ]; then
        echo "shims: $file still has a TEMP-SHIM for $item, which docs/framework-versions.md records as released" >&2
        fail=1
      fi
    done
  done
done < <(grep -R -n --include='*.cs' --include='*.csproj' --include='*.props' 'TEMP-SHIM' src tests tools 2>/dev/null | grep -v -E '/(bin|obj)/' || true)

if [ "$fail" -ne 0 ]; then
  exit 1
fi
echo "shims: ok"

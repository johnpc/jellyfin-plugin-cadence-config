#!/bin/bash
# Fails if any tracked C# source file exceeds the configured max line count.
# Excludes generated/build/test directories.
#
# Usage: scripts/check-line-count.sh [max_lines]

set -e

MAX="${1:-250}"
VIOLATIONS=0

echo ""
echo "Line count check (max: ${MAX} lines per source file)"
echo "============================================================"

while IFS= read -r file; do
    lines=$(wc -l < "$file" | tr -d ' ')
    if [ "$lines" -gt "$MAX" ]; then
        echo "  ${lines} lines  ${file}"
        VIOLATIONS=$((VIOLATIONS + 1))
    fi
done < <(find . -name '*.cs' \
    -not -path '*/bin/*' \
    -not -path '*/obj/*' \
    -not -path '*Tests/*' \
    -not -path '*AcceptanceTests/*')

if [ "$VIOLATIONS" -gt 0 ]; then
    echo ""
    echo "${VIOLATIONS} file(s) exceed ${MAX} lines. Extract helpers/services to split them."
    exit 1
fi

echo ""
echo "All source files within ${MAX} lines."
exit 0

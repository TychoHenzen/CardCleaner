#!/bin/bash
set -euo pipefail

OUTPUT_FILE=".solve-session/dead-code-analysis.txt"
PROJECT_DIR="$(cd "$(dirname "$0")/.." && pwd)"

echo "Running dead code analysis..."
echo "Project: $PROJECT_DIR"
echo "Output: $OUTPUT_FILE"
echo ""

cd "$PROJECT_DIR"

# Run build and capture all output (don't use /warnaserror to avoid build failures)
dotnet build --no-incremental 2>&1 | tee "$OUTPUT_FILE" || true

echo ""
echo "Filtering results..."

# Filter for dead code warnings, excluding addons/ and Tests/
grep -E "IDE0051|IDE0052|CA1822" "$OUTPUT_FILE" | \
  grep -v "addons/" | \
  grep -v "Tests/" | \
  sort | uniq > "${OUTPUT_FILE}.filtered"

UNUSED_COUNT=$(grep -c "IDE0051" "${OUTPUT_FILE}.filtered" 2>/dev/null || echo "0")
UNREAD_COUNT=$(grep -c "IDE0052" "${OUTPUT_FILE}.filtered" 2>/dev/null || echo "0")
STATIC_COUNT=$(grep -c "CA1822" "${OUTPUT_FILE}.filtered" 2>/dev/null || echo "0")

echo "Results:"
echo "  IDE0051 (unused private members): $UNUSED_COUNT"
echo "  IDE0052 (unread private members): $UNREAD_COUNT"
echo "  CA1822 (could be static): $STATIC_COUNT"
echo ""
echo "Filtered output saved to: ${OUTPUT_FILE}.filtered"
echo "Full output saved to: $OUTPUT_FILE"
echo "Review filtered results to identify dead code candidates."

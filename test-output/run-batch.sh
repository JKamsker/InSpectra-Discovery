#!/bin/bash
set -e
FRAMEWORK="$1"
shift
OUTPUT_BASE="test-output/$FRAMEWORK"
mkdir -p "$OUTPUT_BASE"

for SPEC in "$@"; do
  PKG_ID="${SPEC%%@*}"
  VERSION="${SPEC##*@}"
  OUTDIR="$OUTPUT_BASE/$PKG_ID"
  echo ">>> $PKG_ID $VERSION"
  dotnet run --project src/InSpectra.Discovery.Tool -- analysis run-static \
    --package-id "$PKG_ID" --version "$VERSION" \
    --output-root "$OUTDIR" --batch-id "validation" \
    --source "validation" --json \
    --cli-framework "$FRAMEWORK" \
    --install-timeout-seconds 120 \
    --analysis-timeout-seconds 120 \
    --command-timeout-seconds 15 2>&1 | tail -1 || true
  echo ""
done

echo "=== SUMMARY for $FRAMEWORK ==="
for OUTDIR in "$OUTPUT_BASE"/*/; do
  PKG=$(basename "$OUTDIR")
  RESULT="$OUTDIR/result.json"
  if [ -f "$RESULT" ]; then
    DISP=$(node -e "console.log(JSON.parse(require('fs').readFileSync('$RESULT','utf8')).disposition || 'unknown')")
    COV=$(node -e "try{console.log(JSON.parse(require('fs').readFileSync('$RESULT','utf8')).coverage?.coverageMode || 'n/a')}catch(e){console.log('n/a')}")
    OPENCLI="no"
    [ -f "$OUTDIR/opencli.json" ] && OPENCLI="yes"
    echo "  $PKG: disposition=$DISP coverage=$COV opencli=$OPENCLI"
  else
    echo "  $PKG: NO RESULT"
  fi
done

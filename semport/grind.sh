#!/usr/bin/env bash
# semport/grind.sh — burn through the semport ledger backlog
# Usage: ./semport/grind.sh [max_runs]
# Default: 100 runs (~1100 commits at 11/run)
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"

MAX_RUNS="${1:-100}"
RUN=0

while [ "$RUN" -lt "$MAX_RUNS" ]; do
    RUN=$((RUN + 1))
    STATS=$(python3 semport/ledger.py stats 2>&1)
    NEW=$(echo "$STATS" | grep 'new:' | awk '{print $2}')

    echo "=== Run $RUN/$MAX_RUNS | $NEW new commits remaining ==="

    if [ "$NEW" -eq 0 ]; then
        echo "Ledger fully caught up. Done."
        break
    fi

    # Clean up stale state
    rm -f .ai/semport_new_commits.md .ai/semport_plan.md .ai/semport_plan_finalized.md \
          .ai/semport_validation_report.md .ai/semport_failure_analysis.md \
          .ai/semport_impl.log .ai/semport_implementation_summary.md

    # Run pipeline
    if attractor semport/semport.dot --auto-approve 2>&1 | tee /tmp/semport-grind-run.log | tail -5; then
        echo "Pipeline exited cleanly (caught up)"
    else
        REASON=$(grep "Reason:" /tmp/semport-grind-run.log 2>/dev/null | tail -1 || echo "unknown")
        echo "Pipeline exited: $REASON"
    fi

    # Push whatever was committed
    git push 2>/dev/null || true

    # Brief stats
    python3 semport/ledger.py stats
    echo ""

    # Small delay to avoid API rate limits
    sleep 5
done

echo "=== Grind complete ==="
python3 semport/ledger.py stats

#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RESULTS_ROOT="${SCRIPT_DIR}/results"

usage() {
    cat >&2 <<EOF
Usage: $0 <label> <vus> [container_name ...]

  label           Descriptive run label, e.g. threads-sem2-vu5
  vus             Number of concurrent virtual users (one audio upload each)
  container_name  Container(s) to monitor with docker stats
                  (default: audio-processor-api)

Environment overrides:
  BASE_URL                default: http://localhost:8080
  POLL_INTERVAL_MS        default: 1000
  MAX_WAIT_MS             default: 240000
  STATS_INTERVAL_SECONDS  default: 2
  AUDIO_FILE_PATH         default: <this dir>/test-audio/sample.mp3
EOF
    exit 1
}

[ $# -ge 2 ] || usage

LABEL="$1"
VUS="$2"
shift 2
CONTAINERS=("$@")
if [ ${#CONTAINERS[@]} -eq 0 ]; then
    CONTAINERS=("audio-processor-api")
fi

BASE_URL="${BASE_URL:-http://localhost:8080}"
POLL_INTERVAL_MS="${POLL_INTERVAL_MS:-1000}"
MAX_WAIT_MS="${MAX_WAIT_MS:-240000}"
STATS_INTERVAL_SECONDS="${STATS_INTERVAL_SECONDS:-2}"
AUDIO_FILE_PATH="${AUDIO_FILE_PATH:-${SCRIPT_DIR}/test-audio/sample.mp3}"

RESULTS_DIR="${RESULTS_ROOT}/${LABEL}"
mkdir -p "$RESULTS_DIR"
echo "Results will be written to: ${RESULTS_DIR}"

# 2. Start docker stats capture in the background.
"${SCRIPT_DIR}/docker-stats.sh" "${RESULTS_DIR}/docker-stats.csv" "$STATS_INTERVAL_SECONDS" "${CONTAINERS[@]}" &
STATS_PID=$!
echo "$STATS_PID" > "${RESULTS_DIR}/docker-stats.pid"
echo "docker-stats.sh running (PID ${STATS_PID}), monitoring: ${CONTAINERS[*]}"

cleanup_stats() {
    if kill -0 "$STATS_PID" 2>/dev/null; then
        kill "$STATS_PID" 2>/dev/null || true
        wait "$STATS_PID" 2>/dev/null || true
    fi
}
trap cleanup_stats EXIT

# 3. Record start timestamp.
START_TIME=$(date -u +%Y-%m-%dT%H:%M:%SZ)

# 4. Run k6. upload-audio.js defines handleSummary(), which writes
# k6-summary.json and summary.html itself (using RESULTS_DIR below) — that
# makes k6 ignore --summary-export, so it's intentionally not passed here.
set +e
BASE_URL="$BASE_URL" \
POLL_INTERVAL_MS="$POLL_INTERVAL_MS" \
MAX_WAIT_MS="$MAX_WAIT_MS" \
VUS="$VUS" \
AUDIO_FILE_PATH="$AUDIO_FILE_PATH" \
RESULTS_DIR="$RESULTS_DIR" \
k6 run \
    "${SCRIPT_DIR}/upload-audio.js" \
    | tee "${RESULTS_DIR}/k6-output.txt"
K6_EXIT_CODE=${PIPESTATUS[0]}
set -e

# 5. Record end timestamp and run parameters.
END_TIME=$(date -u +%Y-%m-%dT%H:%M:%SZ)
{
    echo "label=${LABEL}"
    echo "vus=${VUS}"
    echo "containers=${CONTAINERS[*]}"
    echo "base_url=${BASE_URL}"
    echo "poll_interval_ms=${POLL_INTERVAL_MS}"
    echo "max_wait_ms=${MAX_WAIT_MS}"
    echo "start=${START_TIME}"
    echo "end=${END_TIME}"
    echo "k6_exit_code=${K6_EXIT_CODE}"
} > "${RESULTS_DIR}/timing.txt"

# 6. Stop docker stats (also runs via the EXIT trap if something above failed).
cleanup_stats
trap - EXIT

# 7. Confirm where everything landed.
echo ""
echo "Benchmark '${LABEL}' finished (k6 exit code: ${K6_EXIT_CODE})."
echo "Results:"
echo "  ${RESULTS_DIR}/k6-summary.json"
echo "  ${RESULTS_DIR}/summary.html"
echo "  ${RESULTS_DIR}/k6-output.txt"
echo "  ${RESULTS_DIR}/docker-stats.csv"
echo "  ${RESULTS_DIR}/timing.txt"

exit "$K6_EXIT_CODE"

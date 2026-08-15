#!/usr/bin/env bash
set -euo pipefail

# Periodically samples `docker stats` for one or more containers into a CSV.
# Meant to be launched in the background by run-benchmark.sh and stopped by
# killing its PID — it has no built-in stop condition of its own.
#
# Usage: docker-stats.sh <output_csv> <interval_seconds> <container> [container ...]

if [ $# -lt 3 ]; then
    echo "Usage: $0 <output_csv> <interval_seconds> <container> [container ...]" >&2
    exit 1
fi

OUTPUT_CSV="$1"
INTERVAL_SECONDS="$2"
shift 2
CONTAINERS=("$@")

echo "timestamp,container,cpu_percent,mem_usage,mem_percent" > "$OUTPUT_CSV"

while true; do
    TIMESTAMP=$(date -u +%Y-%m-%dT%H:%M:%SZ)
    docker stats --no-stream --format '{{.Name}},{{.CPUPerc}},{{.MemUsage}},{{.MemPerc}}' "${CONTAINERS[@]}" 2>/dev/null \
        | while IFS=, read -r name cpu mem_usage mem_percent; do
            echo "${TIMESTAMP},${name},${cpu},\"${mem_usage}\",${mem_percent}" >> "$OUTPUT_CSV"
        done
    sleep "$INTERVAL_SECONDS"
done

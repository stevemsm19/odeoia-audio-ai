#!/usr/bin/env python3
"""CPU-only local summarization worker.

Reads the transcript from stdin (never persisted to disk) and prints one
JSON object to stdout. See worker_common for logging setup.
"""
import argparse
import json
import re
import subprocess
import sys
import time
from pathlib import Path

from worker_common import configure_logging, run_subprocess, compact_text

logger = configure_logging("audio_summary_worker")

# With a 500-char summary target, 256 output tokens, and prompt overhead,
# ~20 000 characters of transcript safely fits within an 8 192-token context.
MAX_TRANSCRIPT_CHARS = 20_000
MAX_SUMMARY_CHARS = 500


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--llama-cli", required=True)
    parser.add_argument("--llm-model", required=True)
    args = parser.parse_args()

    logger.info("Summarization worker started")

    if not Path(args.llm_model).is_file():
        logger.error("Required file not found: %s", args.llm_model)
        raise FileNotFoundError(f"Required local file was not found: {args.llm_model}")

    transcript = sys.stdin.read().strip()
    if not transcript:
        logger.info("Empty transcript received, nothing to summarize")
        print(json.dumps({"summary": "No speech detected", "elapsedMilliseconds": 0}))
        return 0

    if len(transcript) > MAX_TRANSCRIPT_CHARS:
        transcript = transcript[:MAX_TRANSCRIPT_CHARS].rsplit(" ", 1)[0] + " [truncated]"

    prompt = (
        f"Summarize this speech in at most {MAX_SUMMARY_CHARS} characters. Return only the "
        "summary, without labels, quotes, or explanation.\nSpeech:\n" + transcript
    )
    logger.debug("Prompt: %s", prompt)

    started = time.perf_counter()
    result = run_subprocess(logger, [
        args.llama_cli, "-m", args.llm_model, "-p", prompt,
        "-n", "256", "-c", "8192", "-t", "2", "--temp", "0",
        "--no-display-prompt", "--no-conversation", "--single-turn", "--simple-io", "--log-disable",
    ])
    elapsed_ms = round((time.perf_counter() - started) * 1000)

    logger.debug("Raw LLM output: %s", result.stdout)

    # llama.cpp prints a small terminal UI before the generated response. The
    # response is the final non-empty line before its throughput report.
    lines = [line.strip() for line in result.stdout.splitlines()]
    report_index = next((index for index, line in reversed(list(enumerate(lines))) if line.startswith("[ Prompt:")), len(lines))
    generated = next((line for line in reversed(lines[:report_index]) if line and not line.startswith(">")), "")
    generated = re.sub(r"^(summary|answer)\s*:\s*", "", generated, flags=re.IGNORECASE)

    final_summary = compact_text(generated, max_length=MAX_SUMMARY_CHARS)
    logger.info("Summarization finished in %d ms (%d characters)", elapsed_ms, len(final_summary))

    print(json.dumps({"summary": final_summary, "elapsedMilliseconds": elapsed_ms}, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except subprocess.CalledProcessError as error:
        logger.error("Subprocess failed with exit code %s", error.returncode)
        print(error.stderr, file=sys.stderr)
        raise SystemExit(error.returncode or 1)
    except Exception as error:
        logger.error("Unhandled exception: %s", error)
        print(str(error), file=sys.stderr)
        raise SystemExit(1)

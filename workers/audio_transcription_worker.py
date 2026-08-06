#!/usr/bin/env python3
"""CPU-only local speech-to-text worker.

Prints one JSON object to stdout for the .NET host to consume. See
worker_common for logging setup.
"""
import argparse
import json
import subprocess
import sys
import tempfile
import time
from pathlib import Path

from worker_common import configure_logging, run_subprocess, compact_text

logger = configure_logging("audio_transcription_worker")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True)
    parser.add_argument("--whisper-cli", required=True)
    parser.add_argument("--whisper-model", required=True)
    args = parser.parse_args()

    logger.info("Transcription worker started for %s", args.input)

    for path in (args.input, args.whisper_model):
        if not Path(path).is_file():
            logger.error("Required file not found: %s", path)
            raise FileNotFoundError(f"Required local file was not found: {path}")

    with tempfile.TemporaryDirectory(prefix="audio-transcription-worker-") as directory:
        directory_path = Path(directory)
        wav_path = directory_path / "normalized.wav"
        transcript_prefix = directory_path / "transcript"

        # Normalization makes whisper.cpp input predictable and keeps memory low.
        run_subprocess(logger, [
            "ffmpeg", "-y", "-i", args.input, "-ac", "1", "-ar", "16000", "-c:a", "pcm_s16le", str(wav_path),
        ])

        started = time.perf_counter()
        run_subprocess(logger, [
            args.whisper_cli, "-m", args.whisper_model, "-f", str(wav_path),
            "-l", "en", "-t", "2", "-ng", "-nt", "-otxt", "-of", str(transcript_prefix),
        ])
        elapsed_ms = round((time.perf_counter() - started) * 1000)

        transcript_path = transcript_prefix.with_suffix(".txt")
        raw_transcript = transcript_path.read_text(encoding="utf-8").strip() if transcript_path.exists() else ""
        transcript = compact_text(raw_transcript)

        logger.info("Transcription finished in %d ms (%d characters)", elapsed_ms, len(transcript))

        print(json.dumps({"transcript": transcript, "elapsedMilliseconds": elapsed_ms}, ensure_ascii=False))
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

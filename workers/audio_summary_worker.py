#!/usr/bin/env python3
"""CPU-only local STT and short-summary worker.

The worker intentionally writes only one JSON object to stdout so the .NET host
can consume it safely. Tool diagnostics remain on stderr.
"""
import argparse
import json
import re
import subprocess
import sys
import tempfile
import time
import os
import datetime
from pathlib import Path


def log_debug(stage: str, data: dict):
    log_entry = {
        "timestamp": datetime.datetime.now().isoformat(),
        "stage": stage,
        **data
    }
    print(json.dumps(log_entry), file=sys.stderr)


def run(command: list[str]) -> subprocess.CompletedProcess[str]:
    start = time.perf_counter()
    log_debug("Executing Command", {"command": command, "cwd": os.getcwd()})
    result = subprocess.run(command, check=False, text=True, capture_output=True)
    elapsed = time.perf_counter() - start
    log_debug("Command Finished", {
        "command": command,
        "exit_code": result.returncode,
        "elapsed_seconds": elapsed,
        "stdout": result.stdout,
        "stderr": result.stderr
    })
    result.check_returncode()
    return result


def compact(text: str) -> str:
    text = " ".join(text.replace("\x00", " ").split())
    text = text.strip(" \t\n\r\"'")
    if not text:
        return "No speech detected"
    if len(text) <= 500:
        return text
    truncated = text[:499].rstrip()
    boundary = truncated.rfind(" ")
    if boundary > 15:
        truncated = truncated[:boundary]
    return truncated + "…"


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True)
    parser.add_argument("--whisper-cli", required=True)
    parser.add_argument("--whisper-model", required=True)
    parser.add_argument("--llama-cli", required=True)
    parser.add_argument("--llm-model", required=True)
    args = parser.parse_args()

    log_debug("Worker Started", {"args": vars(args)})

    for path in (args.input, args.whisper_model, args.llm_model):
        if not Path(path).is_file():
            log_debug("File Not Found", {"path": path})
            raise FileNotFoundError(f"Required local file was not found: {path}")

    with tempfile.TemporaryDirectory(prefix="audio-summary-worker-") as directory:
        directory_path = Path(directory)
        wav_path = directory_path / "normalized.wav"
        transcript_prefix = directory_path / "transcript"

        # Normalization makes whisper.cpp input predictable and keeps memory low.
        run(["ffmpeg", "-y", "-i", args.input, "-ac", "1", "-ar", "16000", "-c:a", "pcm_s16le", str(wav_path)])
        speech_to_text_started = time.perf_counter()
        run([
            args.whisper_cli, "-m", args.whisper_model, "-f", str(wav_path),
            "-l", "en", "-t", "2", "-ng", "-nt", "-otxt", "-of", str(transcript_prefix),
        ])
        speech_to_text_elapsed_ms = round((time.perf_counter() - speech_to_text_started) * 1000)

        transcript_path = transcript_prefix.with_suffix(".txt")
        transcript = transcript_path.read_text(encoding="utf-8").strip() if transcript_path.exists() else ""
        log_debug("Whisper Transcription", {"transcript": transcript})
        if not transcript:
            log_debug("Pipeline Finished", {"reason": "No speech detected"})
            print(json.dumps({"summary": "No speech detected", "speechToTextElapsedMilliseconds": speech_to_text_elapsed_ms, "summarizationElapsedMilliseconds": 0}))
            return 0

        prompt = (
            "Summarize this speech in at most 500 characters. Return only the summary, "
            "without labels, quotes, or explanation.\nSpeech:\n" + transcript
        )
        log_debug("LLM Prompt Generation", {"prompt": prompt})
        summarization_started = time.perf_counter()
        result = run([
            args.llama_cli, "-m", args.llm_model, "-p", prompt,
            "-n", "256", "-c", "2048", "-t", "2", "--temp", "0",
            "--no-display-prompt", "--no-conversation", "--single-turn", "--simple-io", "--log-disable",
        ])
        
        log_debug("LLM Raw Response", {"raw_output": result.stdout})
        
        # llama.cpp prints a small terminal UI before the generated response. The
        # response is the final non-empty line before its throughput report.
        lines = [line.strip() for line in result.stdout.splitlines()]
        report_index = next((index for index, line in reversed(list(enumerate(lines))) if line.startswith("[ Prompt:")), len(lines))
        generated = next((line for line in reversed(lines[:report_index]) if line and not line.startswith(">")), "")
        generated = re.sub(r"^(summary|answer)\s*:\s*", "", generated, flags=re.IGNORECASE)
        summarization_elapsed_ms = round((time.perf_counter() - summarization_started) * 1000)
        
        final_summary = compact(generated)
        response_json = {"summary": final_summary, "speechToTextElapsedMilliseconds": speech_to_text_elapsed_ms, "summarizationElapsedMilliseconds": summarization_elapsed_ms}
        log_debug("Pipeline Finished", {"parsed_summary": final_summary, "json_response": response_json})
        
        print(json.dumps(response_json, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except subprocess.CalledProcessError as error:
        log_debug("Process Error", {"stderr": error.stderr, "exit_code": error.returncode})
        print(error.stderr, file=sys.stderr)
        raise SystemExit(error.returncode or 1)
    except Exception as error:
        log_debug("Unhandled Exception", {"error": str(error)})
        print(str(error), file=sys.stderr)
        raise SystemExit(1)

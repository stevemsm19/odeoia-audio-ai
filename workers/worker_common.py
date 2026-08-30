"""Shared helpers for the local STT/LLM worker scripts.

WORKER_LOG_LEVEL controls verbosity (default INFO); full subprocess
output and LLM prompts are only logged at DEBUG.
"""
import logging
import os
import subprocess
import sys
import time


def configure_logging(name: str) -> logging.Logger:
    level_name = os.environ.get("WORKER_LOG_LEVEL", "INFO").upper()
    level = getattr(logging, level_name, logging.INFO)
    logging.basicConfig(
        level=level,
        format="%(asctime)s %(levelname)s %(name)s: %(message)s",
        stream=sys.stderr,
    )
    return logging.getLogger(name)


def run_subprocess(logger: logging.Logger, command: list[str]) -> subprocess.CompletedProcess[str]:
    logger.info("Running command: %s", command[0])
    logger.debug("Full command: %s", command)
    start = time.perf_counter()
    result = subprocess.run(command, check=False, text=True, capture_output=True)
    elapsed = time.perf_counter() - start
    logger.info("Command %s finished in %.2fs with exit code %s", command[0], elapsed, result.returncode)
    logger.debug("stdout: %s", result.stdout)
    logger.debug("stderr: %s", result.stderr)
    result.check_returncode()
    return result


def compact_text(text: str, max_length: int | None = None) -> str:
    """Collapse whitespace and, optionally, cap length at a word boundary."""
    normalized = " ".join((text or "").replace("\x00", " ").split())
    normalized = normalized.strip(" \t\n\r\"'")
    if not normalized:
        return "No speech detected"
    if max_length is None or len(normalized) <= max_length:
        return normalized

    truncated = normalized[: max_length - 1].rstrip()
    boundary = truncated.rfind(" ")
    if boundary > 15:
        truncated = truncated[:boundary]
    return truncated + "…"

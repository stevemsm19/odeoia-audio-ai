#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MODEL_DIR="${ROOT_DIR}/models"

# These are publisher-maintained repositories: ggml-org's converted Whisper
# assets and Qwen's official GGUF release.
WHISPER_FILE="ggml-tiny.en-q5_1.bin"
WHISPER_URL="https://huggingface.co/ggerganov/whisper.cpp/resolve/main/${WHISPER_FILE}?download=true"
WHISPER_SHA256="c77c5766f1cef09b6b7d47f21b546cbddd4157886b3b5d6d4f709e91e66c7c2b"
QWEN_FILE="qwen2.5-0.5b-instruct-q5_k_m.gguf"
QWEN_URL="https://huggingface.co/Qwen/Qwen2.5-0.5B-Instruct-GGUF/resolve/main/${QWEN_FILE}?download=true"

is_valid_model() {
    local path="$1"
    local expected_sha256="${2:-}"
    [[ -s "${path}" ]] || return 1

    if [[ -n "${expected_sha256}" ]]; then
        command -v shasum >/dev/null 2>&1 || return 0
        [[ "$(shasum -a 256 "${path}" | awk '{print $1}')" == "${expected_sha256}" ]]
    fi
}

download_if_missing() {
    local file_name="$1"
    local url="$2"
    local expected_sha256="${3:-}"
    local destination="${MODEL_DIR}/${file_name}"

    if is_valid_model "${destination}" "${expected_sha256}"; then
        echo "Model already available: ${destination}"
        return 0
    fi

    if ! command -v curl >/dev/null 2>&1; then
        echo "curl is required to provision local AI models." >&2
        exit 1
    fi

    local temporary="${destination}.download"
    rm -f "${temporary}" "${destination}"
    echo "Downloading ${file_name} from its official publisher..."
    curl --fail --location --retry 3 --retry-delay 2 --output "${temporary}" "${url}"
    is_valid_model "${temporary}" "${expected_sha256}" || { echo "Downloaded model failed integrity validation: ${file_name}" >&2; rm -f "${temporary}"; exit 1; }
    mv "${temporary}" "${destination}"
    echo "Downloaded ${destination}"
}

mkdir -p "${MODEL_DIR}"
download_if_missing "${WHISPER_FILE}" "${WHISPER_URL}" "${WHISPER_SHA256}"
download_if_missing "${QWEN_FILE}" "${QWEN_URL}"

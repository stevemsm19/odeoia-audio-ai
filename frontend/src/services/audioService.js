const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || "/api";
const POLL_INTERVAL_MS = 2_000;
const POLL_TIMEOUT_MS = 10 * 60 * 1_000;

export class AudioProcessingError extends Error {
  constructor(message, { isCanceled = false } = {}) {
    super(message);
    this.name = "AudioProcessingError";
    this.isCanceled = isCanceled;
  }
}

const readError = async (response) => {
  try {
    const body = await response.json();
    return body.error || body.title || null;
  } catch {
    return null;
  }
};

const request = async (path, options = {}) => {
  let response;
  try {
    response = await fetch(`${API_BASE_URL}${path}`, options);
  } catch (error) {
    if (error.name === "AbortError") {
      throw new AudioProcessingError("The audio request was canceled.", {
        isCanceled: true,
      });
    }
    throw new AudioProcessingError(
      "We couldn't connect to the audio processing service. Please try again.",
    );
  }

  if (!response.ok) {
    const message = await readError(response);
    throw new AudioProcessingError(
      message ||
        "The audio processing service could not complete your request.",
    );
  }

  return response.json();
};

const TERMINAL_STATUSES = new Set(["Completed", "Failed"]);

const normalizeStatus = (response) => ({
  id: response.id,
  originalFileName: response.fileName,
  processedAudioUrl: response.compressedStorageUrl,
  compressionStatus: response.compressionStatus,
  summaryStatus: response.summaryStatus,
  summary: response.summary ?? null,
  summaryError: response.summaryError ?? null,
});

const sleep = (signal) =>
  new Promise((resolve, reject) => {
    const timeoutId = window.setTimeout(resolve, POLL_INTERVAL_MS);
    signal?.addEventListener(
      "abort",
      () => {
        window.clearTimeout(timeoutId);
        reject(
          new AudioProcessingError("The audio request was canceled.", {
            isCanceled: true,
          }),
        );
      },
      { once: true },
    );
  });

export async function processAudio(file, { signal, onStatus }) {
  const formData = new FormData();
  formData.append("file", file);

  onStatus?.("Uploading audio…");
  const upload = await request("/audio/upload", {
    method: "POST",
    body: formData,
    signal,
  });
  const startedAt = Date.now();

  while (Date.now() - startedAt < POLL_TIMEOUT_MS) {
    const result = normalizeStatus(
      await request(`/audio/${upload.id}`, { signal }),
    );

    const compressionDone = TERMINAL_STATUSES.has(result.compressionStatus);
    const summaryDone = TERMINAL_STATUSES.has(result.summaryStatus);

    if (compressionDone && summaryDone) {
      if (result.compressionStatus === "Failed") {
        throw new AudioProcessingError(
          "We couldn't compress this audio file. Please try another file.",
        );
      }
      if (!result.processedAudioUrl) {
        throw new AudioProcessingError(
          "The audio was processed, but no compressed file is available.",
        );
      }
      return result;
    }

    if (!compressionDone) {
      onStatus?.("Compressing audio…");
    } else {
      onStatus?.("Generating summary…");
    }

    await sleep(signal);
  }

  throw new AudioProcessingError(
    "Audio processing is taking longer than expected. Please try again later.",
  );
}

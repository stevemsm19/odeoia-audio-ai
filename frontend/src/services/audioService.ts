const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || "/api";
const POLL_INTERVAL_MS = 2_000;
const POLL_TIMEOUT_MS = 10 * 60 * 1_000;

export type OverallStatus = "Pending" | "Processing" | "Completed";
export type StageStatus = "Pending" | "Processing" | "Completed" | "Failed";

export class AudioProcessingError extends Error {
  isCanceled: boolean;

  constructor(message: string, { isCanceled = false }: { isCanceled?: boolean } = {}) {
    super(message);
    this.name = "AudioProcessingError";
    this.isCanceled = isCanceled;
  }
}

type AudioUploadApiResponse = {
  id: string;
  fileName: string;
  storageUrl: string;
  status: string;
  createdAt: string;
};

// Mirrors AudioProcessingStatusResponse (backend/src/AudioProcessor.Api/Controllers/AudioController.cs).
// Keep in sync — contract changes must land in the same change as the frontend.
type AudioStatusApiResponse = {
  id: string;
  fileName: string;
  overallStatus: OverallStatus;
  compressionStatus: StageStatus;
  compressedStorageUrl: string | null;
  compressionDurationMs: number | null;
  transcriptionStatus: StageStatus;
  transcriptionError: string | null;
  transcriptionDurationMs: number | null;
  summaryStatus: StageStatus;
  summaryStorageUrl: string | null;
  summaryError: string | null;
  summaryDurationMs: number | null;
};

const readError = async (response: Response): Promise<string | null> => {
  try {
    const body = await response.json();
    return body.error || body.title || null;
  } catch {
    return null;
  }
};

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}${path}`, options);
  } catch (error) {
    if (error instanceof DOMException && error.name === "AbortError") {
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

  return response.json() as Promise<T>;
}

const sleep = (signal?: AbortSignal): Promise<void> =>
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

/** Per-stage snapshot, updated on every poll tick — drives the live pipeline view. */
export type AudioProcessingSnapshot = {
  overallStatus: OverallStatus;
  compressionStatus: StageStatus;
  transcriptionStatus: StageStatus;
  summaryStatus: StageStatus;
};

export type AudioProcessingResult = AudioProcessingSnapshot & {
  id: string;
  originalFileName: string;
  /** Null when compression didn't succeed — never assume it's present. */
  processedAudioUrl: string | null;
  /** Fetched from summaryStorageUrl once available; null if summary failed,
   * hasn't produced a file yet, or the file itself couldn't be fetched. */
  summaryText: string | null;
  summaryError: string | null;
};

const toSnapshot = (response: AudioStatusApiResponse): AudioProcessingSnapshot => ({
  overallStatus: response.overallStatus,
  compressionStatus: response.compressionStatus,
  transcriptionStatus: response.transcriptionStatus,
  summaryStatus: response.summaryStatus,
});

async function fetchSummaryText(summaryStorageUrl: string, signal?: AbortSignal): Promise<string | null> {
  try {
    const response = await fetch(summaryStorageUrl, { signal });
    if (!response.ok) return null;
    return await response.text();
  } catch {
    return null;
  }
}

type ProcessAudioOptions = {
  signal?: AbortSignal;
  onStatus?: (snapshot: AudioProcessingSnapshot) => void;
};

export async function processAudio(
  file: File,
  { signal, onStatus }: ProcessAudioOptions,
): Promise<AudioProcessingResult> {
  const formData = new FormData();
  formData.append("file", file);

  const upload = await request<AudioUploadApiResponse>("/audio/upload", {
    method: "POST",
    body: formData,
    signal,
  });
  const startedAt = Date.now();

  while (Date.now() - startedAt < POLL_TIMEOUT_MS) {
    const response = await request<AudioStatusApiResponse>(`/audio/${upload.id}`, { signal });
    const snapshot = toSnapshot(response);
    onStatus?.(snapshot);

    // "Completed" means every stage reached a terminal state — not a success verdict.
    if (snapshot.overallStatus === "Completed") {
      const summaryText = response.summaryStorageUrl
        ? await fetchSummaryText(response.summaryStorageUrl, signal)
        : null;

      return {
        ...snapshot,
        id: response.id,
        originalFileName: response.fileName,
        processedAudioUrl: response.compressedStorageUrl,
        summaryText,
        summaryError: response.summaryError,
      };
    }

    await sleep(signal);
  }

  throw new AudioProcessingError(
    "Audio processing is taking longer than expected. Please try again later.",
  );
}

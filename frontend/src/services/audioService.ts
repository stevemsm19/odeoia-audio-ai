import * as signalR from "@microsoft/signalr";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || "/api";
const HUB_URL = `${API_BASE_URL}/hubs/audio-status`;
const PROCESSING_TIMEOUT_MS = 10 * 60 * 1_000;

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

export function toAacFileName(originalFileName: string): string {
  return originalFileName.replace(/\.[^.]+$/, ".aac");
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
  noiseReductionStatus: StageStatus;
  filteredStorageUrl: string | null;
  noiseReductionError: string | null;
  noiseReductionDurationMs: number | null;
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

/** Per-stage snapshot, updated on every status push — drives the live pipeline view. */
export type AudioProcessingSnapshot = {
  overallStatus: OverallStatus;
  compressionStatus: StageStatus;
  transcriptionStatus: StageStatus;
  summaryStatus: StageStatus;
  noiseReductionStatus: StageStatus;
};

export type AudioProcessingResult = AudioProcessingSnapshot & {
  id: string;
  originalFileName: string;
  /** Null when compression didn't succeed — never assume it's present. */
  processedAudioUrl: string | null;
  /** Null when noise reduction hasn't completed (still processing, or failed). */
  filteredAudioUrl: string | null;
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
  noiseReductionStatus: response.noiseReductionStatus,
});

export async function fetchSummaryText(summaryStorageUrl: string, signal?: AbortSignal): Promise<string | null> {
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
  email?: string;
};

export async function processAudio(
  file: File,
  { signal, onStatus, email }: ProcessAudioOptions,
): Promise<AudioProcessingResult> {
  const formData = new FormData();
  formData.append("file", file);
  if (email) formData.append("email", email);

  const upload = await request<AudioUploadApiResponse>("/audio/upload", {
    method: "POST",
    body: formData,
    signal,
  });

  const connection = new signalR.HubConnectionBuilder()
    .withUrl(HUB_URL)
    .withAutomaticReconnect()
    .build();

  return new Promise<AudioProcessingResult>((resolve, reject) => {
    let settled = false;

    const cleanup = () => {
      window.clearTimeout(timeoutId);
      signal?.removeEventListener("abort", onAbort);
      void connection.stop();
    };

    const settleReject = (error: Error) => {
      if (settled) return;
      settled = true;
      cleanup();
      reject(error);
    };

    const settleResolve = (result: AudioProcessingResult) => {
      if (settled) return;
      settled = true;
      cleanup();
      resolve(result);
    };

    const timeoutId = window.setTimeout(() => {
      settleReject(
        new AudioProcessingError("Audio processing is taking longer than expected. Please try again later."),
      );
    }, PROCESSING_TIMEOUT_MS);

    const onAbort = () => {
      settleReject(new AudioProcessingError("The audio request was canceled.", { isCanceled: true }));
    };
    signal?.addEventListener("abort", onAbort, { once: true });

    const fetchAndReport = async () => {
      if (settled) return;

      const response = await request<AudioStatusApiResponse>(`/audio/${upload.id}`, { signal });
      const snapshot = toSnapshot(response);
      onStatus?.(snapshot);

      // "Completed" means every stage reached a terminal state — not a success verdict.
      if (snapshot.overallStatus === "Completed") {
        const summaryText = response.summaryStorageUrl
          ? await fetchSummaryText(response.summaryStorageUrl, signal)
          : null;

        settleResolve({
          ...snapshot,
          id: response.id,
          originalFileName: response.fileName,
          processedAudioUrl: response.compressedStorageUrl,
          filteredAudioUrl: response.filteredStorageUrl,
          summaryText,
          summaryError: response.summaryError,
        });
      }
    };

    connection.on("audioStatusChanged", () => {
      fetchAndReport().catch(settleReject);
    });

    connection
      .start()
      .then(() => connection.invoke("SubscribeToAudio", upload.id))
      .then(() => fetchAndReport())
      .catch((error) => {
        settleReject(
          error instanceof AudioProcessingError
            ? error
            : new AudioProcessingError("We couldn't connect to the audio processing service. Please try again."),
        );
      });
  });
}

// Mirrors AudioListItemResponse (backend/src/AudioProcessor.Api/Controllers/AudioController.cs).
type AudioListApiResponse = {
  id: string;
  fileName: string;
  storageUrl: string;
  compressedStorageUrl: string | null;
  overallStatus: OverallStatus;
  summaryStorageUrl: string | null;
  createdAt: string;
  filteredStorageUrl: string | null;
};

export type AudioListItem = {
  id: string;
  fileName: string;
  storageUrl: string;
  compressedStorageUrl: string | null;
  overallStatus: OverallStatus;
  summaryStorageUrl: string | null;
  createdAt: string;
  filteredStorageUrl: string | null;
};

export async function listAudios(email: string, signal?: AbortSignal): Promise<AudioListItem[]> {
  const items = await request<AudioListApiResponse[]>(
    `/audio?email=${encodeURIComponent(email)}`,
    { signal },
  );
  return items.map((item) => ({ ...item }));
}

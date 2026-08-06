import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { AudioProcessingError, processAudio } from "./audioService";

function jsonResponse(body: unknown, init: { ok?: boolean; status?: number } = {}) {
  const ok = init.ok ?? true;
  return {
    ok,
    status: init.status ?? (ok ? 200 : 500),
    json: async () => body,
    text: async () => (typeof body === "string" ? body : JSON.stringify(body)),
  } as Response;
}

describe("processAudio", () => {
  const file = new File(["fake-bytes"], "clip.mp3", { type: "audio/mpeg" });

  beforeEach(() => {
    vi.stubGlobal("fetch", vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.useRealTimers();
  });

  it("resolves with the per-stage snapshot and fetched summary text once overallStatus is Completed", async () => {
    const fetchMock = vi.mocked(fetch);
    fetchMock
      // POST /api/audio/upload
      .mockResolvedValueOnce(
        jsonResponse({
          id: "audio-1",
          fileName: "clip.mp3",
          storageUrl: "https://example.supabase.co/audio-files/clip.mp3",
          status: "Pending",
          createdAt: "2026-01-01T00:00:00Z",
        }),
      )
      // GET /api/audio/audio-1 — already Completed on first poll
      .mockResolvedValueOnce(
        jsonResponse({
          id: "audio-1",
          fileName: "clip.mp3",
          overallStatus: "Completed",
          compressionStatus: "Completed",
          compressedStorageUrl: "https://example.supabase.co/audio-files/clip.aac",
          transcriptionStatus: "Completed",
          transcriptionError: null,
          summaryStatus: "Completed",
          summaryStorageUrl: "https://example.supabase.co/summaries/summary.txt",
          summaryError: null,
        }),
      )
      // GET the summary .txt directly from Storage
      .mockResolvedValueOnce(jsonResponse("A concise summary of the recording.", { ok: true }));

    const onStatus = vi.fn();
    const result = await processAudio(file, { onStatus });

    expect(result.processedAudioUrl).toBe("https://example.supabase.co/audio-files/clip.aac");
    expect(result.summaryText).toBe("A concise summary of the recording.");
    expect(result.compressionStatus).toBe("Completed");
    expect(result.transcriptionStatus).toBe("Completed");
    expect(result.summaryStatus).toBe("Completed");
    expect(onStatus).toHaveBeenCalledWith(
      expect.objectContaining({ overallStatus: "Completed" }),
    );
  });

  it("never fetches a summary when summaryStorageUrl is absent", async () => {
    const fetchMock = vi.mocked(fetch);
    fetchMock
      .mockResolvedValueOnce(
        jsonResponse({ id: "audio-2", fileName: "clip.mp3", storageUrl: "x", status: "Pending", createdAt: "x" }),
      )
      .mockResolvedValueOnce(
        jsonResponse({
          id: "audio-2",
          fileName: "clip.mp3",
          overallStatus: "Completed",
          compressionStatus: "Failed",
          compressedStorageUrl: null,
          transcriptionStatus: "Completed",
          transcriptionError: null,
          summaryStatus: "Failed",
          summaryStorageUrl: null,
          summaryError: "llama-cli exited with 1",
        }),
      );

    const result = await processAudio(file, {});

    expect(result.summaryText).toBeNull();
    expect(result.summaryError).toBe("llama-cli exited with 1");
    expect(result.processedAudioUrl).toBeNull();
    // Only 2 fetch calls: upload + one status poll — no third call for a summary file.
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });

  it("reports intermediate snapshots via onStatus before the flow completes", async () => {
    vi.useFakeTimers();
    const fetchMock = vi.mocked(fetch);
    fetchMock
      .mockResolvedValueOnce(
        jsonResponse({ id: "audio-3", fileName: "clip.mp3", storageUrl: "x", status: "Pending", createdAt: "x" }),
      )
      .mockResolvedValueOnce(
        jsonResponse({
          id: "audio-3",
          fileName: "clip.mp3",
          overallStatus: "Processing",
          compressionStatus: "Processing",
          compressedStorageUrl: null,
          transcriptionStatus: "Pending",
          transcriptionError: null,
          summaryStatus: "Pending",
          summaryStorageUrl: null,
          summaryError: null,
        }),
      )
      .mockResolvedValueOnce(
        jsonResponse({
          id: "audio-3",
          fileName: "clip.mp3",
          overallStatus: "Completed",
          compressionStatus: "Completed",
          compressedStorageUrl: "https://example.supabase.co/audio-files/clip.aac",
          transcriptionStatus: "Completed",
          transcriptionError: null,
          summaryStatus: "Completed",
          summaryStorageUrl: null,
          summaryError: null,
        }),
      );

    const onStatus = vi.fn();
    const resultPromise = processAudio(file, { onStatus });

    await vi.advanceTimersByTimeAsync(2_000);
    const result = await resultPromise;

    expect(onStatus).toHaveBeenNthCalledWith(1, expect.objectContaining({ overallStatus: "Processing" }));
    expect(onStatus).toHaveBeenNthCalledWith(2, expect.objectContaining({ overallStatus: "Completed" }));
    expect(result.overallStatus).toBe("Completed");
  });

  it("throws AudioProcessingError when the upload request fails", async () => {
    const fetchMock = vi.mocked(fetch);
    fetchMock.mockResolvedValueOnce(
      jsonResponse({ error: "Invalid file type." }, { ok: false, status: 400 }),
    );

    await expect(processAudio(file, {})).rejects.toBeInstanceOf(AudioProcessingError);
  });
});

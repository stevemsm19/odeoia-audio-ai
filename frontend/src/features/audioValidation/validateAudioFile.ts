import { resampleTo16kMono } from "./resampleAudio";
import { sniffAudioSignature } from "./signatureCheck";
import type { ValidationResult, WorkerRequestMessage } from "./types";

export type ValidationHandle = {
  promise: Promise<ValidationResult>;
  cancel: () => void;
};

/**
 * Validates an audio file before it's uploaded: checks its file signature,
 * decodes it, and delegates the heavy computation (speech detection) to a
 * Web Worker via a transferable ArrayBuffer.
 */
export function validateAudioFile(file: File): ValidationHandle {
  let worker: Worker | null = null;
  let canceled = false;

  const promise = (async (): Promise<ValidationResult> => {
    const header = new Uint8Array(await file.slice(0, 12).arrayBuffer());
    if (sniffAudioSignature(header) === "unknown") {
      return { valid: false, reason: "This file doesn't look like a real MP3 or WAV audio file." };
    }
    if (canceled) return { valid: false, reason: "Validation canceled." };

    const arrayBuffer = await file.arrayBuffer();
    const audioContext = new AudioContext();
    let audioBuffer: AudioBuffer;
    try {
      audioBuffer = await audioContext.decodeAudioData(arrayBuffer);
    } catch {
      return { valid: false, reason: "This file couldn't be decoded as audio." };
    } finally {
      audioContext.close();
    }
    if (canceled) return { valid: false, reason: "Validation canceled." };

    const channelData: Float32Array[] = [];
    for (let channel = 0; channel < audioBuffer.numberOfChannels; channel++) {
      channelData.push(audioBuffer.getChannelData(channel));
    }
    const resampled = resampleTo16kMono(channelData, audioBuffer.sampleRate);

    return new Promise<ValidationResult>((resolve) => {
      worker = new Worker(new URL("../../workers/audioValidation.worker.ts", import.meta.url), {
        type: "module",
      });

      worker.onmessage = (event: MessageEvent<ValidationResult>) => {
        resolve(event.data);
        worker?.terminate();
      };

      worker.onerror = () => {
        resolve({ valid: false, reason: "Audio validation failed unexpectedly." });
        worker?.terminate();
      };

      const message: WorkerRequestMessage = { pcm: resampled.buffer, sampleRate: 16_000 };
      worker.postMessage(message, [resampled.buffer]);
    });
  })();

  return {
    promise,
    cancel: () => {
      canceled = true;
      worker?.terminate();
    },
  };
}

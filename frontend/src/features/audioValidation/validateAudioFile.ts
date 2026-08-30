import {
  pipeline,
  type AutomaticSpeechRecognitionPipeline,
} from "@huggingface/transformers";
import { resampleTo16kMono } from "./resampleAudio";
import { sniffAudioSignature } from "./signatureCheck";
import type { ValidationResult } from "./types";

export type ValidationHandle = {
  promise: Promise<ValidationResult>;
  cancel: () => void;
};

const MIN_TRANSCRIPT_LENGTH = 10;

// Lazily loaded once per page load — the model is fetched/cached by the
// browser on first use, and reused for every subsequent validation. Runs on
// the main thread (sequential baseline: no Web Worker offload).
let transcriberPromise: Promise<AutomaticSpeechRecognitionPipeline> | null =
  null;

function getTranscriber() {
  if (!transcriberPromise) {
    transcriberPromise = pipeline(
      "automatic-speech-recognition",
      "Xenova/whisper-tiny",
    ) as unknown as Promise<AutomaticSpeechRecognitionPipeline>;
  }
  return transcriberPromise;
}

/**
 * Validates an audio file before it's uploaded: checks its file signature,
 * decodes it, resamples it, and transcribes it (speech detection) — all on
 * the main thread, sequentially.
 */
export function validateAudioFile(file: File): ValidationHandle {
  let canceled = false;

  const promise = (async (): Promise<ValidationResult> => {
    const header = new Uint8Array(await file.slice(0, 12).arrayBuffer());
    if (sniffAudioSignature(header) === "unknown") {
      return {
        valid: false,
        reason: "This file doesn't look like a real MP3 or WAV audio file.",
      };
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
    if (canceled) return { valid: false, reason: "Validation canceled." };

    try {
      const transcriber = await getTranscriber();
      if (canceled) return { valid: false, reason: "Validation canceled." };

      const output = await transcriber(resampled);
      const text = Array.isArray(output) ? output[0]?.text ?? "" : output.text;
      const transcript = text.trim();

      return transcript.length < MIN_TRANSCRIPT_LENGTH
        ? { valid: false, reason: "No speech detected in this audio." }
        : { valid: true, reason: "" };
    } catch (error) {
      return {
        valid: false,
        reason:
          error instanceof Error ? error.message : "Audio validation failed.",
      };
    }
  })();

  return {
    promise,
    cancel: () => {
      canceled = true;
    },
  };
}

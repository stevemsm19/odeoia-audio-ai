/// <reference lib="webworker" />
import { pipeline, type AutomaticSpeechRecognitionPipeline } from "@huggingface/transformers";
import type { WorkerRequestMessage, WorkerResponseMessage } from "../features/audioValidation/types";

const MIN_TRANSCRIPT_LENGTH = 10;

// Lazily loaded once per worker instance — the model is fetched/cached by the
// browser on first use, and reused for every subsequent validation.
let transcriberPromise: Promise<AutomaticSpeechRecognitionPipeline> | null = null;

function getTranscriber() {
  if (!transcriberPromise) {
    transcriberPromise = pipeline("automatic-speech-recognition", "Xenova/whisper-tiny") as unknown as Promise<AutomaticSpeechRecognitionPipeline>;
  }
  return transcriberPromise;
}

self.onmessage = async (event: MessageEvent<WorkerRequestMessage>) => {
  const { pcm } = event.data;

  try {
    const transcriber = await getTranscriber();
    const samples = new Float32Array(pcm);
    const output = await transcriber(samples);
    const text = Array.isArray(output) ? output[0]?.text ?? "" : output.text;
    const transcript = text.trim();

    const result: WorkerResponseMessage =
      transcript.length < MIN_TRANSCRIPT_LENGTH
        ? { valid: false, reason: "No speech detected in this audio." }
        : { valid: true, reason: "" };

    self.postMessage(result);
  } catch (error) {
    const result: WorkerResponseMessage = {
      valid: false,
      reason: error instanceof Error ? error.message : "Audio validation failed.",
    };
    self.postMessage(result);
  }
};

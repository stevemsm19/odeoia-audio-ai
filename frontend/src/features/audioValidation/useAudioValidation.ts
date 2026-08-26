import { useEffect, useRef, useState } from "react";
import { getAudioType } from "./audioSignature";

export type AudioValidationStatus =
  | "idle"
  | "checking-signature"
  | "decoding"
  | "loading-model"
  | "transcribing"
  | "valid"
  | "invalid"
  | "error";

export interface AudioValidationState {
  status: AudioValidationStatus;
  reason?: string;
}

export interface UseAudioValidationReturn {
  status: AudioValidationStatus;
  reason?: string;
  validate: (file: File) => Promise<void>;
}

const SAMPLE_RATE = 16000; // Target sample rate for STT
const MIN_TRANSCRIPT_LENGTH = 10; // Minimum character count for "speech detected"

/**
 * Resample audio to target sample rate
 * Mixes all channels to mono and resamples via linear interpolation
 * Returns a Float32Array of mono audio at the target sample rate
 */
async function resampleAudio(
  audioBuffer: AudioBuffer,
  targetSampleRate: number
): Promise<Float32Array> {
  const channels = audioBuffer.numberOfChannels;
  const length = audioBuffer.duration * targetSampleRate;

  // Manually mix channels to mono with resampling
  // This approach is more reliable across browsers than using OfflineAudioContext
  const sourceData = new Float32Array(Math.ceil(length));
  for (let i = 0; i < channels; i++) {
    const channelData = audioBuffer.getChannelData(i);
    // Downsample by averaging
    const ratio = audioBuffer.sampleRate / targetSampleRate;
    for (let j = 0; j < sourceData.length; j++) {
      const sourceIndex = Math.floor(j * ratio);
      if (sourceIndex < channelData.length) {
        sourceData[j] += channelData[sourceIndex] / channels;
      }
    }
  }

  return sourceData;
}

/**
 * Hook for client-side audio validation
 * Implements two-tier validation: magic byte check, then STT inference
 * Manages Web Worker lifecycle and validation state machine
 */
export function useAudioValidation(): UseAudioValidationReturn {
  const [state, setState] = useState<AudioValidationState>({ status: "idle" });
  const workerRef = useRef<Worker | null>(null);
  const abortControllerRef = useRef<AbortController | null>(null);

  /**
   * Create and initialize the worker lazily on first validation
   */
  const getOrCreateWorker = (): Worker => {
    if (!workerRef.current) {
      // Create a module worker for ESM support
      workerRef.current = new Worker(
        new URL("../../workers/audioValidation.worker.ts", import.meta.url),
        { type: "module" }
      );
    }
    return workerRef.current;
  };

  /**
   * Validate audio file through multi-stage pipeline
   */
  const validate = async (file: File): Promise<void> => {
    // Cancel any previous validation
    if (abortControllerRef.current) {
      abortControllerRef.current.abort();
    }

    const controller = new AbortController();
    abortControllerRef.current = controller;

    try {
      // Stage 1: Check audio signature (magic bytes)
      setState({ status: "checking-signature" });

      const arrayBuffer = await file.arrayBuffer();
      const audioType = getAudioType(arrayBuffer);

      if (!audioType) {
        setState({
          status: "invalid",
          reason: "Not a recognized audio format (MP3 or WAV required)",
        });
        return;
      }

      if (controller.signal.aborted) return;

      // Stage 2: Decode audio
      setState({ status: "decoding" });

      const audioContext = new (window.AudioContext || (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext)();
      let audioBuffer: AudioBuffer;

      try {
        audioBuffer = await audioContext.decodeAudioData(arrayBuffer.slice(0));
      } catch {
        setState({
          status: "invalid",
          reason: "Could not decode audio file",
        });
        return;
      }

      if (controller.signal.aborted) return;

      // Stage 3: Resample to 16kHz mono
      setState({ status: "loading-model" });

      const resampledAudio = await resampleAudio(audioBuffer, SAMPLE_RATE);

      if (controller.signal.aborted) return;

      // Stage 4: Send to worker for STT inference
      setState({ status: "transcribing" });

      const worker = getOrCreateWorker();

      // Create a promise that resolves when the worker responds
      const workerResult = await new Promise<{ transcript: string }>((resolve, reject) => {
        const handleMessage = (event: MessageEvent) => {
          const response = event.data;

          if (response.type === "result") {
            worker.removeEventListener("message", handleMessage);
            worker.removeEventListener("error", handleError);
            resolve(response.data);
          } else if (response.type === "error") {
            worker.removeEventListener("message", handleMessage);
            worker.removeEventListener("error", handleError);
            reject(new Error(response.error));
          }
          // Ignore progress messages
        };

        const handleError = (error: ErrorEvent) => {
          worker.removeEventListener("message", handleMessage);
          worker.removeEventListener("error", handleError);
          reject(new Error(`Worker error: ${error.message}`));
        };

        worker.addEventListener("message", handleMessage);
        worker.addEventListener("error", handleError);

        // Transfer the audio buffer to the worker (transfer ownership, not copy)
        worker.postMessage(
          {
            type: "transcribe",
            buffer: resampledAudio,
          },
          [resampledAudio.buffer]
        );
      });

      if (controller.signal.aborted) return;

      // Stage 5: Validate transcript
      const transcript = (workerResult.transcript || "").trim();

      if (transcript.length < MIN_TRANSCRIPT_LENGTH) {
        setState({
          status: "invalid",
          reason: "No speech detected in audio",
        });
      } else {
        setState({
          status: "valid",
          reason: undefined,
        });
      }
    } catch (error) {
      if (error instanceof Error && error.message?.includes("aborted")) {
        // Validation was cancelled, don't update state
        return;
      }

      const errorMessage = error instanceof Error ? error.message : String(error);
      setState({
        status: "invalid",
        reason: `Validation error: ${errorMessage}`,
      });
    }
  };

  // Cleanup worker on unmount
  useEffect(() => {
    return () => {
      if (abortControllerRef.current) {
        abortControllerRef.current.abort();
      }
      if (workerRef.current) {
        workerRef.current.terminate();
        workerRef.current = null;
      }
    };
  }, []);

  return {
    status: state.status,
    reason: state.reason,
    validate,
  };
}

export type ValidationResult = {
  valid: boolean;
  reason: string;
};

export type WorkerRequestMessage = {
  pcm: ArrayBufferLike;
  sampleRate: number;
};

export type WorkerResponseMessage = ValidationResult;

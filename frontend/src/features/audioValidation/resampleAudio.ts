const TARGET_SAMPLE_RATE = 16_000;

/**
 * Mixes down to mono and resamples (linear interpolation) to 16kHz — the
 * sample rate the Whisper model expects. Runs on the main thread since it
 * needs the AudioBuffer's channel data, but is cheap: this only prepares the
 * (small) buffer that gets transferred to the worker for the actual heavy
 * computation (model inference).
 */
export function resampleTo16kMono(channelData: Float32Array[], originalSampleRate: number): Float32Array {
  const channelCount = channelData.length;
  const length = channelData[0]?.length ?? 0;

  const mono = new Float32Array(length);
  for (let i = 0; i < length; i++) {
    let sum = 0;
    for (let channel = 0; channel < channelCount; channel++) {
      sum += channelData[channel][i];
    }
    mono[i] = sum / channelCount;
  }

  if (originalSampleRate === TARGET_SAMPLE_RATE) return mono;

  const ratio = originalSampleRate / TARGET_SAMPLE_RATE;
  const outputLength = Math.round(length / ratio);
  const output = new Float32Array(outputLength);

  for (let i = 0; i < outputLength; i++) {
    const sourceIndex = i * ratio;
    const lowerIndex = Math.floor(sourceIndex);
    const upperIndex = Math.min(lowerIndex + 1, length - 1);
    const weight = sourceIndex - lowerIndex;
    output[i] = mono[lowerIndex] * (1 - weight) + mono[upperIndex] * weight;
  }

  return output;
}

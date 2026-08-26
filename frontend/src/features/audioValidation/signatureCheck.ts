export type AudioSignature = "mp3" | "wav" | "unknown";

/**
 * Sniffs the first bytes of a file against known audio magic numbers, so a
 * renamed .zip (or any non-audio file) is rejected before ever being decoded.
 */
export function sniffAudioSignature(bytes: Uint8Array): AudioSignature {
  if (bytes.length >= 12) {
    const isRiff =
      bytes[0] === 0x52 && bytes[1] === 0x49 && bytes[2] === 0x46 && bytes[3] === 0x46; // "RIFF"
    const isWave =
      bytes[8] === 0x57 && bytes[9] === 0x41 && bytes[10] === 0x56 && bytes[11] === 0x45; // "WAVE"
    if (isRiff && isWave) return "wav";
  }

  if (bytes.length >= 3) {
    const isId3 = bytes[0] === 0x49 && bytes[1] === 0x44 && bytes[2] === 0x33; // "ID3"
    if (isId3) return "mp3";
  }

  if (bytes.length >= 2) {
    const isFrameSync =
      bytes[0] === 0xff && (bytes[1] === 0xfb || bytes[1] === 0xf3 || bytes[1] === 0xf2);
    if (isFrameSync) return "mp3";
  }

  return "unknown";
}

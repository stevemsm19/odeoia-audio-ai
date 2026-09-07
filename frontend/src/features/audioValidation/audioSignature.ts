/**
 * Audio signature detection via magic byte sniffing.
 * Runs synchronously on the main thread for cheap early validation.
 */

/**
 * Detects audio type by inspecting magic bytes (WAV/MP3 headers).
 */
export function getAudioType(buffer: ArrayBuffer): "wav" | "mp3" | null {
  if (buffer.byteLength < 4) {
    return null;
  }

  const view = new Uint8Array(buffer);

  // WAV: RIFF header (bytes 0-3) + WAVE marker (bytes 8-11)
  if (
    view[0] === 0x52 &&
    view[1] === 0x49 &&
    view[2] === 0x46 &&
    view[3] === 0x46
  ) {
    if (buffer.byteLength >= 12) {
      if (
        view[8] === 0x57 &&
        view[9] === 0x41 &&
        view[10] === 0x56 &&
        view[11] === 0x45
      ) {
        return "wav";
      }
    }
  }

  // MP3: ID3 tag ("ID3")
  if (view[0] === 0x49 && view[1] === 0x44 && view[2] === 0x33) {
    return "mp3";
  }

  // MP3: MPEG sync frame (0xFF + 111xxxxx)
  if (view[0] === 0xff) {
    const syncByte = view[1];
    if ((syncByte & 0xe0) === 0xe0) {
      return "mp3";
    }
  }

  return null;
}

/**
 * Audio signature detection via magic byte sniffing.
 * Runs synchronously on the main thread for cheap early validation.
 */

/**
 * Detect audio file type by inspecting magic bytes
 * WAV: RIFF + WAVE
 * MP3: ID3 tag or MPEG sync frame (0xFF 0xFB, 0xFF 0xFA, 0xFF 0xF3, 0xFF 0xF2)
 *
 * @param buffer - ArrayBuffer of file data
 * @returns Audio type or null if not recognized
 */
export function getAudioType(buffer: ArrayBuffer): "wav" | "mp3" | null {
  if (buffer.byteLength < 4) {
    return null;
  }

  const view = new Uint8Array(buffer);

  // Check for WAV: RIFF header (bytes 0-3) + WAVE marker (bytes 8-11)
  if (
    view[0] === 0x52 && // R
    view[1] === 0x49 && // I
    view[2] === 0x46 && // F
    view[3] === 0x46    // F
  ) {
    if (buffer.byteLength >= 12) {
      if (
        view[8] === 0x57 && // W
        view[9] === 0x41 && // A
        view[10] === 0x56 && // V
        view[11] === 0x45    // E
      ) {
        return "wav";
      }
    }
  }

  // Check for MP3: ID3 tag (0x49 0x44 0x33 = "ID3")
  if (view[0] === 0x49 && view[1] === 0x44 && view[2] === 0x33) {
    return "mp3";
  }

  // Check for MPEG sync frame (0xFF followed by a sync-specific byte)
  // Sync bytes: 0xFB, 0xFA, 0xF3, 0xF2 (various MPEG-1/2/3 layer combinations)
  if (view[0] === 0xff) {
    const syncByte = view[1];
    // Check for MPEG audio frame sync (bits: 11111111 111xxxxx)
    if ((syncByte & 0xe0) === 0xe0) {
      return "mp3";
    }
  }

  return null;
}

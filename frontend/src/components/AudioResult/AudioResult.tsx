import { AudioPlayer } from "../AudioPlayer/AudioPlayer";
import "./AudioResult.css";

export type AudioResultSummary = {
  text: string | null;
  error?: string | null;
};

type AudioResultProps = {
  fileName: string;
  audioUrl: string | null;
  downloadFileName?: string;
  filteredAudioUrl?: string | null;
  filteredDownloadFileName?: string;
  size?: "large" | "compact";
  summary: AudioResultSummary;
};

/**
 * Shared "processed audio" view: player(s), download link(s), and summary. Used at
 * "large" size for HomePage's single result and "compact" size for each row
 * in the List Audios page — same shape, different scale. The file name is
 * shown by AudioPlayer itself, so no separate heading/badge is needed here.
 */
export function AudioResult({
  fileName,
  audioUrl,
  downloadFileName,
  filteredAudioUrl,
  filteredDownloadFileName,
  size = "compact",
  summary,
}: AudioResultProps) {
  return (
    <div className={`audio-result audio-result-${size}`}>
      {audioUrl ? (
        <>
          <AudioPlayer src={audioUrl} fileName={downloadFileName ?? fileName} label="Compressed audio" />
          <a className="inline-link" href={audioUrl} download>
            Download
          </a>
        </>
      ) : (
        <div className="audio-result-heading">
          <strong>{fileName}</strong>
          <p className="stage-unavailable">No processed audio available.</p>
        </div>
      )}
      {filteredAudioUrl && (
        <>
          <AudioPlayer
            src={filteredAudioUrl}
            fileName={filteredDownloadFileName ?? fileName}
            label="Noise-reduced audio"
          />
          <a className="inline-link" href={filteredAudioUrl} download>
            Download
          </a>
        </>
      )}
      <article className="audio-result-summary">
        <span className="summary-icon">✦</span>
        <div>
          <h4>Summary</h4>
          <p>{summary.text || summary.error || "No summary available for this audio."}</p>
        </div>
      </article>
    </div>
  );
}

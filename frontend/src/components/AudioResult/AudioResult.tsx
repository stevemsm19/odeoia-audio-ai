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
  size?: "large" | "compact";
  summary: AudioResultSummary;
};

/**
 * Shared "processed audio" view: player, download link, and summary. Used at
 * "large" size for HomePage's single result and "compact" size for each row
 * in the List Audios page — same shape, different scale. The file name is
 * shown by AudioPlayer itself, so no separate heading/badge is needed here.
 */
export function AudioResult({
  fileName,
  audioUrl,
  downloadFileName,
  size = "compact",
  summary,
}: AudioResultProps) {
  return (
    <div className={`audio-result audio-result-${size}`}>
      {audioUrl ? (
        <>
          <AudioPlayer src={audioUrl} fileName={downloadFileName ?? fileName} />
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

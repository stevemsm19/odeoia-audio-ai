import type { StageStatus } from "../../services/audioService";
import "./PipelineStatus.css";

type RowState = StageStatus | "NotImplemented" | "Waiting";

const ICONS: Record<RowState, string> = {
  Pending: "○",
  Processing: "⏳",
  Completed: "✔",
  Failed: "✕",
  NotImplemented: "▢",
  Waiting: "⏸",
};

const LABELS: Record<RowState, string> = {
  Pending: "Pending",
  Processing: "In progress",
  Completed: "Completed",
  Failed: "Failed",
  NotImplemented: "Not implemented yet",
  Waiting: "Waiting for transcription",
};

type PipelineStatusProps = {
  compressionStatus: StageStatus;
  transcriptionStatus: StageStatus;
  summaryStatus: StageStatus;
};

export function PipelineStatus({
  compressionStatus,
  transcriptionStatus,
  summaryStatus,
}: PipelineStatusProps) {
  // Noise filtering stays visible with a fixed state even though it isn't implemented yet.
  const summaryState: RowState =
    summaryStatus === "Pending" && transcriptionStatus !== "Completed" ? "Waiting" : summaryStatus;

  const stages: Array<{ key: string; label: string; state: RowState }> = [
    { key: "compression", label: "Compression", state: compressionStatus },
    { key: "filter", label: "Noise Filtering", state: "NotImplemented" },
    { key: "transcription", label: "Transcription", state: transcriptionStatus },
    { key: "summary", label: "Summary", state: summaryState },
  ];

  return (
    <section className="pipeline-status" aria-live="polite">
      <ul className="pipeline-list">
        {stages.map((stage) => (
          <li key={stage.key} className={`pipeline-row pipeline-row--${stage.state.toLowerCase()}`}>
            <span className="pipeline-icon" aria-hidden="true">
              {ICONS[stage.state]}
            </span>
            <span className="pipeline-label">{stage.label}</span>
            <span className="pipeline-state">{LABELS[stage.state]}</span>
          </li>
        ))}
      </ul>
    </section>
  );
}

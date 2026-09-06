import type { StageStatus } from "../../services/audioService";
import "./PipelineStatus.css";

const ICONS: Record<StageStatus, string> = {
  Pending: "○",
  Processing: "⏳",
  Completed: "✔",
  Failed: "✕",
};

const LABELS: Record<StageStatus, string> = {
  Pending: "Pending",
  Processing: "In progress",
  Completed: "Completed",
  Failed: "Failed",
};

type PipelineStatusProps = {
  compressionStatus: StageStatus;
  noiseReductionStatus: StageStatus;
  transcriptionStatus: StageStatus;
  summaryStatus: StageStatus;
};

export function PipelineStatus({
  compressionStatus,
  noiseReductionStatus,
  transcriptionStatus,
  summaryStatus,
}: PipelineStatusProps) {
  const stages: Array<{ key: string; label: string; state: StageStatus }> = [
    { key: "compression", label: "Compression", state: compressionStatus },
    { key: "filter", label: "Noise Filtering", state: noiseReductionStatus },
    { key: "transcription", label: "Transcription", state: transcriptionStatus },
    { key: "summary", label: "Summary", state: summaryStatus },
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

import "./ProgressBar.css";
export function ProgressBar({ stage }) {
  return (
    <section className="progress-card" aria-live="polite">
      <div className="progress-heading">
        <span>
          <span className="pulse" />
          {stage}
        </span>
        <strong>In progress</strong>
      </div>
      <div
        className="progress-track"
        role="progressbar"
        aria-label="Audio processing is in progress"
      >
        <span />
      </div>
    </section>
  );
}

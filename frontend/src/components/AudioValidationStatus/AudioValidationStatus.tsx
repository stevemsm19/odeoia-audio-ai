import type { AudioValidationStatus } from "../../features/audioValidation/useAudioValidation";
import "./AudioValidationStatus.css";

type ValidatorRowState = AudioValidationStatus | "Waiting";

const ICONS: Record<ValidatorRowState, string> = {
  "idle": "○",
  "checking-signature": "⏳",
  "decoding": "⏳",
  "loading-model": "⏳",
  "transcribing": "⏳",
  "valid": "✔",
  "invalid": "✕",
  "error": "✕",
  "Waiting": "⏸",
};

const LABELS: Record<ValidatorRowState, string> = {
  "idle": "Idle",
  "checking-signature": "Checking audio format",
  "decoding": "Decoding audio",
  "loading-model": "Loading speech model",
  "transcribing": "Analyzing speech",
  "valid": "Valid audio",
  "invalid": "Invalid audio",
  "error": "Error",
  "Waiting": "Waiting",
};

type AudioValidationStatusProps = {
  status: AudioValidationStatus;
  reason?: string;
};

export function AudioValidationStatus({
  status,
  reason,
}: AudioValidationStatusProps) {
  return (
    <div className="audio-validation-status" aria-live="polite">
      <div className={`validation-row validation-row--${status.replace(/-/g, "")}`}>
        <span className="validation-icon" aria-hidden="true">
          {ICONS[status]}
        </span>
        <div className="validation-content">
          <span className="validation-label">{LABELS[status]}</span>
          {reason && <span className="validation-reason">{reason}</span>}
        </div>
      </div>
    </div>
  );
}

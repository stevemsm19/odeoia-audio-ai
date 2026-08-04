import { useRef, useState } from "react";
import "./FileUpload.css";
const accepted = ["audio/mpeg", "audio/wav", "audio/x-wav"];
const valid = (file) =>
  file && (accepted.includes(file.type) || /\.(mp3|wav)$/i.test(file.name));
const size = (bytes) =>
  bytes < 1024 * 1024
    ? `${Math.max(1, Math.round(bytes / 1024))} KB`
    : `${(bytes / 1024 / 1024).toFixed(1)} MB`;
export function FileUpload({ file, onSelect, onRemove, disabled }) {
  const input = useRef();
  const [dragging, setDragging] = useState(false);
  const [error, setError] = useState("");
  const pick = (candidate) => {
    if (!candidate) return;
    if (!valid(candidate)) {
      setError("Choose an MP3 or WAV audio file.");
      return;
    }
    setError("");
    onSelect(candidate);
  };
  if (file)
    return (
      <section className="selected-file">
        <div className="file-icon">♫</div>
        <div>
          <strong>{file.name}</strong>
          <p>
            {file.type || "Audio file"} · {size(file.size)}
          </p>
        </div>
        <button
          className="remove-button"
          type="button"
          onClick={onRemove}
          disabled={disabled}
          aria-label="Remove selected file"
        >
          ×
        </button>
      </section>
    );
  return (
    <div
      className={`dropzone ${dragging ? "dragging" : ""} ${error ? "invalid" : ""}`}
      onDragOver={(event) => {
        event.preventDefault();
        setDragging(true);
      }}
      onDragLeave={() => setDragging(false)}
      onDrop={(event) => {
        event.preventDefault();
        setDragging(false);
        pick(event.dataTransfer.files[0]);
      }}
    >
      <input
        ref={input}
        type="file"
        accept=".mp3,.wav,audio/mpeg,audio/wav"
        onChange={(event) => pick(event.target.files[0])}
      />
      <div className="upload-icon">↑</div>
      <h3>Drop your audio here</h3>
      <p>
        or{" "}
        <button type="button" onClick={() => input.current?.click()}>
          browse from your device
        </button>
      </p>
      <small>MP3 or WAV · Maximum 100 MB</small>
      {error && (
        <p className="field-error" role="alert">
          {error}
        </p>
      )}
    </div>
  );
}

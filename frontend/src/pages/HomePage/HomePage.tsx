import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { AudioResult } from "../../components/AudioResult/AudioResult";
import { Button } from "../../components/Button/Button";
import { FileUpload } from "../../components/FileUpload/FileUpload";
import { PipelineStatus } from "../../components/PipelineStatus/PipelineStatus";
import { useAuth } from "../../features/auth/AuthContext";
import {
  validateAudioFile,
  type ValidationHandle,
} from "../../features/audioValidation/validateAudioFile";
import {
  AudioProcessingError,
  processAudio,
  toAacFileName,
  type AudioProcessingResult,
  type AudioProcessingSnapshot,
} from "../../services/audioService";
import "./HomePage.css";

type ViewState = "idle" | "selected" | "processing" | "completed" | "error";
type ValidationState = "idle" | "checking" | "valid" | "invalid";

const hasAnyFailure = (result: AudioProcessingResult): boolean =>
  result.compressionStatus === "Failed" ||
  result.transcriptionStatus === "Failed" ||
  result.summaryStatus === "Failed" ||
  result.noiseReductionStatus === "Failed";

export function HomePage() {
  const { user } = useAuth();
  const [file, setFile] = useState<File | null>(null);
  const [state, setState] = useState<ViewState>("idle");
  const [pipeline, setPipeline] = useState<AudioProcessingSnapshot | null>(
    null,
  );
  const [result, setResult] = useState<AudioProcessingResult | null>(null);
  const [error, setError] = useState("");
  const [validation, setValidation] = useState<ValidationState>("idle");
  const [validationReason, setValidationReason] = useState("");
  const requestController = useRef<AbortController | null>(null);
  const validationHandle = useRef<ValidationHandle | null>(null);

  const cancelProcessing = () => requestController.current?.abort();
  const cancelValidation = () => validationHandle.current?.cancel();
  useEffect(
    () => () => {
      requestController.current?.abort();
      cancelValidation();
    },
    [],
  );

  const selectFile = (next: File) => {
    cancelProcessing();
    cancelValidation();
    setFile(next);
    setResult(null);
    setPipeline(null);
    setError("");
    setState("selected");
    setValidation("checking");
    setValidationReason("");

    const handle = validateAudioFile(next);
    validationHandle.current = handle;
    handle.promise.then((result) => {
      if (validationHandle.current !== handle) return; // superseded by a newer selection
      setValidation(result.valid ? "valid" : "invalid");
      setValidationReason(result.reason);
    });
  };

  const removeFile = () => {
    cancelProcessing();
    cancelValidation();
    setFile(null);
    setResult(null);
    setPipeline(null);
    setError("");
    setState("idle");
    setValidation("idle");
    setValidationReason("");
  };

  const start = async () => {
    if (!file) return;
    const controller = new AbortController();
    requestController.current = controller;
    setError("");
    setState("processing");

    try {
      const completed = await processAudio(file, {
        signal: controller.signal,
        onStatus: setPipeline,
        email: user?.email,
      });
      setResult(completed);
      setState("completed");
    } catch (processingError) {
      if (
        processingError instanceof AudioProcessingError &&
        processingError.isCanceled
      )
        return;
      setError(
        (processingError instanceof Error && processingError.message) ||
          "Something went wrong while processing the audio. Please try again.",
      );
      setState("error");
    } finally {
      requestController.current = null;
    }
  };

  return (
    <>
      <section className="hero">
        <div>
          <span className="eyebrow">AUDIO, MADE ACTIONABLE</span>
          <h1>
            Clear audio. <em>Clearer</em> thinking.
          </h1>
          <p>Refine your recordings and get an instant, concise summary</p>
          <a href="#workspace" className="hero-link">
            Start processing <span>→</span>
          </a>
        </div>
        <div className="hero-art" aria-hidden="true">
          <div className="wave">
            <i />
            <i />
            <i />
            <i />
            <i />
            <i />
            <i />
            <i />
            <i />
          </div>
          <span>LISTEN. REFINE. UNDERSTAND.</span>
        </div>
      </section>
      <section id="workspace" className="workspace">
        <div className="workspace-title">
          <div>
            <span className="eyebrow">YOUR WORKSPACE</span>
            <h2>Process an audio file</h2>
          </div>
          {user ? (
            <p className="signed-in">
              Working as <strong>{user.email}</strong>
            </p>
          ) : (
            <p>
              <Link to="/login">Log in</Link> to keep your workflow ready for
              future integrations.
            </p>
          )}
        </div>
        <div className="tool-grid">
          <section className="tool-card upload-card">
            <div className="card-heading">
              <span className="step-number">01</span>
              <div>
                <h3>Choose an audio file</h3>
                <p>We support MP3 and WAV files.</p>
              </div>
            </div>
            <FileUpload
              file={file}
              onSelect={selectFile}
              onRemove={removeFile}
              disabled={state === "processing"}
            />
            {state === "selected" && validation === "checking" && (
              <p className="validation-status">Checking audio…</p>
            )}
            {state === "selected" && validation === "invalid" && (
              <div className="error-box" role="alert">
                {validationReason || "This audio file couldn't be validated."}
                <button onClick={removeFile}>Choose another file</button>
              </div>
            )}
            {state === "selected" && validation === "valid" && (
              <Button className="wide" onClick={start}>
                Process audio <span>→</span>
              </Button>
            )}
            {(state === "processing" || state === "completed") && pipeline && (
              <PipelineStatus
                compressionStatus={pipeline.compressionStatus}
                noiseReductionStatus={pipeline.noiseReductionStatus}
                transcriptionStatus={pipeline.transcriptionStatus}
                summaryStatus={pipeline.summaryStatus}
              />
            )}
            {state === "error" && (
              <div className="error-box" role="alert">
                {error} <button onClick={start}>Try again</button>
                <p className="retry-hint">
                  Trying again reprocesses the full audio from the start — it
                  does not resume from where it left off.
                </p>
              </div>
            )}
          </section>
          <section className="tool-card result-card">
            <div className="card-heading">
              <span className="step-number">02</span>
              <div>
                <h3>Your result</h3>
                <p>Ready when processing is complete.</p>
              </div>
            </div>
            {state === "completed" && result ? (
              <div className="result-content">
                {hasAnyFailure(result) ? (
                  <div className="error-box" role="alert">
                    Some steps didn't complete successfully — see the breakdown
                    above for which one.{" "}
                    <button onClick={start}>Try again</button>
                    <p className="retry-hint">
                      Trying again reprocesses the full audio from the start —
                      it does not resume from where it left off.
                    </p>
                  </div>
                ) : (
                  <div className="success-banner">
                    <span>✓</span>
                    <div>
                      <strong>Your audio is ready</strong>
                      <p>All steps completed successfully.</p>
                    </div>
                  </div>
                )}
                <AudioResult
                  size="large"
                  fileName={result.originalFileName}
                  audioUrl={result.processedAudioUrl}
                  downloadFileName={toAacFileName(result.originalFileName)}
                  filteredAudioUrl={result.filteredAudioUrl}
                  filteredDownloadFileName={toAacFileName(result.originalFileName).replace(
                    /\.aac$/,
                    "-filtered.aac",
                  )}
                  summary={{
                    text: result.summaryText,
                    error: result.summaryError,
                  }}
                />
                <Button className="secondary wide" onClick={removeFile}>
                  Process another file
                </Button>
              </div>
            ) : (
              <div className="empty-result">
                <div>✦</div>
                <h3>Your result will appear here</h3>
                <p>Select an audio file and let OdeoIA process it.</p>
              </div>
            )}
          </section>
        </div>
      </section>
    </>
  );
}

import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { AudioPlayer } from "../../components/AudioPlayer/AudioPlayer";
import { Button } from "../../components/Button/Button";
import { FileUpload } from "../../components/FileUpload/FileUpload";
import { PipelineStatus } from "../../components/PipelineStatus/PipelineStatus";
import { useAuth } from "../../features/auth/AuthContext";
import {
  AudioProcessingError,
  processAudio,
  type AudioProcessingResult,
  type AudioProcessingSnapshot,
} from "../../services/audioService";
import "./HomePage.css";

type ViewState = "idle" | "selected" | "processing" | "completed" | "error";

const hasAnyFailure = (result: AudioProcessingResult): boolean =>
  result.compressionStatus === "Failed" ||
  result.transcriptionStatus === "Failed" ||
  result.summaryStatus === "Failed";

export function HomePage() {
  const { user } = useAuth();
  const [file, setFile] = useState<File | null>(null);
  const [state, setState] = useState<ViewState>("idle");
  const [pipeline, setPipeline] = useState<AudioProcessingSnapshot | null>(null);
  const [result, setResult] = useState<AudioProcessingResult | null>(null);
  const [error, setError] = useState("");
  const requestController = useRef<AbortController | null>(null);

  const cancelProcessing = () => requestController.current?.abort();
  useEffect(() => () => requestController.current?.abort(), []);

  const selectFile = (next: File) => {
    cancelProcessing();
    setFile(next);
    setResult(null);
    setPipeline(null);
    setError("");
    setState("selected");
  };

  const removeFile = () => {
    cancelProcessing();
    setFile(null);
    setResult(null);
    setPipeline(null);
    setError("");
    setState("idle");
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
            {state === "selected" && (
              <Button className="wide" onClick={start}>
                Process audio <span>→</span>
              </Button>
            )}
            {(state === "processing" || state === "completed") && pipeline && (
              <PipelineStatus
                compressionStatus={pipeline.compressionStatus}
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
                    Some steps didn't complete successfully — see the
                    breakdown above for which one.{" "}
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
                {result.processedAudioUrl ? (
                  <AudioPlayer
                    src={result.processedAudioUrl}
                    fileName={result.originalFileName.replace(/\.[^.]+$/, ".aac")}
                  />
                ) : (
                  <p className="stage-unavailable">
                    Compression failed — no processed audio to play.
                  </p>
                )}
                <article className="summary">
                  <span className="summary-icon">✦</span>
                  <div>
                    <h4>Summary</h4>
                    <p>
                      {result.summaryText ||
                        result.summaryError ||
                        "No summary available for this audio"}
                    </p>
                    <small>Shown from the current IA capability.</small>
                  </div>
                </article>
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

import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { AudioPlayer } from "../../components/AudioPlayer/AudioPlayer";
import { Button } from "../../components/Button/Button";
import { FileUpload } from "../../components/FileUpload/FileUpload";
import { ProgressBar } from "../../components/ProgressBar/ProgressBar";
import { useAuth } from "../../features/auth/AuthContext";
import {
  AudioProcessingError,
  processAudio,
} from "../../services/audioService";
import "./HomePage.css";

export function HomePage() {
  const { user } = useAuth();
  const [file, setFile] = useState(null);
  const [state, setState] = useState("idle");
  const [processingStage, setProcessingStage] = useState("Preparing audio…");
  const [result, setResult] = useState(null);
  const [error, setError] = useState("");
  const requestController = useRef(null);

  const cancelProcessing = () => requestController.current?.abort();
  useEffect(() => () => requestController.current?.abort(), []);

  const selectFile = (next) => {
    cancelProcessing();
    setFile(next);
    setResult(null);
    setError("");
    setProcessingStage("Preparing audio…");
    setState("selected");
  };

  const removeFile = () => {
    cancelProcessing();
    setFile(null);
    setResult(null);
    setError("");
    setState("idle");
  };

  const start = async () => {
    const controller = new AbortController();
    requestController.current = controller;
    setError("");
    setState("processing");

    try {
      const completed = await processAudio(file, {
        signal: controller.signal,
        onStatus: setProcessingStage,
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
        processingError.message ||
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
            {state === "processing" && <ProgressBar stage={processingStage} />}
            {state === "error" && (
              <div className="error-box" role="alert">
                {error} <button onClick={start}>Try again</button>
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
            {state === "completed" ? (
              <div className="result-content">
                <div className="success-banner">
                  <span>✓</span>
                  <div>
                    <strong>Your audio is ready</strong>
                    <p>Compression completed successfully.</p>
                  </div>
                </div>
                <AudioPlayer
                  src={result.processedAudioUrl}
                  fileName={result.originalFileName.replace(/\.[^.]+$/, ".aac")}
                />
                <article className="summary">
                  <span className="summary-icon">✦</span>
                  <div>
                    <h4>Summary</h4>
                    <p>
                      {result.summary ||
                        result.summaryError ||
                        "No summary available for this audio"}
                    </p>
                    <small>Shown from the current backend capability.</small>
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

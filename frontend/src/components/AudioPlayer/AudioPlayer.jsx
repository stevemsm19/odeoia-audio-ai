import "./AudioPlayer.css";
export function AudioPlayer({ src, fileName }) {
  return (
    <section className="audio-player">
      <div className="file-icon">♫</div>
      <div>
        <strong>Processed audio</strong>
        <p>{fileName}</p>
      </div>
      <audio controls src={src} aria-label="Processed audio player" />
    </section>
  );
}

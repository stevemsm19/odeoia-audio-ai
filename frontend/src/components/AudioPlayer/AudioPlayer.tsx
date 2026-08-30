import "./AudioPlayer.css";

type AudioPlayerProps = {
  src: string;
  fileName: string;
};

export function AudioPlayer({ src, fileName }: AudioPlayerProps) {
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

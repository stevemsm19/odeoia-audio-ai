import "./AudioPlayer.css";

type AudioPlayerProps = {
  src: string;
  fileName: string;
  label?: string;
};

export function AudioPlayer({ src, fileName, label = "Processed audio" }: AudioPlayerProps) {
  return (
    <section className="audio-player">
      <div className="file-icon">♫</div>
      <div>
        <strong>{label}</strong>
        <p>{fileName}</p>
      </div>
      <audio controls src={src} aria-label={`${label} player`} />
    </section>
  );
}

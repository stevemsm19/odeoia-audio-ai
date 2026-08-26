import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { AudioResult } from "../../components/AudioResult/AudioResult";
import { Button } from "../../components/Button/Button";
import { useAuth } from "../../features/auth/AuthContext";
import {
  fetchSummaryText,
  listAudios,
  type AudioListItem,
} from "../../services/audioService";
import "./AudiosPage.css";

export function AudiosPage() {
  const { user } = useAuth();
  const [items, setItems] = useState<AudioListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [summaries, setSummaries] = useState<Record<string, string | null>>({});

  const loadAudios = useCallback(
    (signal?: AbortSignal) => {
      if (!user) return;
      listAudios(user.email, signal)
        .then((records) => {
          setItems(records);
          setSummaries({});
          for (const record of records) {
            if (!record.summaryStorageUrl) continue;
            fetchSummaryText(record.summaryStorageUrl, signal).then((text) => {
              if (signal?.aborted) return;
              setSummaries((prev) => ({ ...prev, [record.id]: text }));
            });
          }
        })
        .catch((fetchError) => {
          if (signal?.aborted) return;
          setError(
            (fetchError instanceof Error && fetchError.message) ||
              "We couldn't load your audios. Please try again.",
          );
        })
        .finally(() => {
          if (!signal?.aborted) setLoading(false);
        });
    },
    [user],
  );

  const handleRefresh = () => {
    setLoading(true);
    setError("");
    loadAudios();
  };

  useEffect(() => {
    const controller = new AbortController();
    loadAudios(controller.signal);
    return () => controller.abort();
  }, [loadAudios]);

  if (!user) {
    return (
      <section className="audios-page">
        <div className="audios-empty">
          <h1>List Audios</h1>
          <p>
            <Link to="/login">Log in</Link> to view the audios associated with
            your account.
          </p>
        </div>
      </section>
    );
  }

  return (
    <section className="audios-page">
      <div className="audios-header">
        <div>
          <span className="eyebrow">YOUR LIBRARY</span>
          <h1>List Audios</h1>
          <p>Every audio you've uploaded as {user.email}.</p>
        </div>
        <Button
          className="secondary"
          onClick={handleRefresh}
          loading={loading}
        >
          Refresh
        </Button>
      </div>

      {error && (
        <div className="error-box" role="alert">
          {error}
        </div>
      )}

      {!loading && items.length === 0 && !error ? (
        <div className="audios-empty">
          <p>No audios uploaded yet. Process one from the home page.</p>
        </div>
      ) : (
        <ul className="audios-list">
          {items.map((item) => (
            <li key={item.id} className="audio-item">
              <AudioResult
                size="compact"
                fileName={item.fileName}
                audioUrl={item.compressedStorageUrl ?? item.storageUrl}
                summary={{ text: summaries[item.id] ?? null }}
              />
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

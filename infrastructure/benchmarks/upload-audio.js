import http from "k6/http";
import { check, sleep } from "k6";
import { Trend } from "k6/metrics";
// Pinned to a release tag (not "main") so the HTML report layout can't
// change out from under a benchmark run without an explicit version bump.
import { htmlReport } from "https://raw.githubusercontent.com/benc-uk/k6-reporter/3.0.4/dist/bundle.js";
import { textSummary } from "https://jslib.k6.io/k6-summary/0.0.3/index.js";

// Each VU simulates one person uploading one audio and waiting for the full
// pipeline (Compression + Transcription->Summary) to reach "Completed".

const BASE_URL = __ENV.BASE_URL || "http://localhost:8080";
const AUDIO_FILE_PATH = __ENV.AUDIO_FILE_PATH || "./test-audio/sample.mp3";
const POLL_INTERVAL_MS = parseInt(__ENV.POLL_INTERVAL_MS || "1000", 10);
// The pipeline runs ~8-9s under normal conditions for the default test audio;
// this default leaves generous headroom for slower runs under load.
const MAX_WAIT_MS = parseInt(__ENV.MAX_WAIT_MS || "240000", 10);
const VUS = parseInt(__ENV.VUS || "1", 10);
// Where handleSummary() writes k6-summary.json/summary.html — k6 resolves
// these relative to its own working directory, not the results folder, so
// run-benchmark.sh passes this in to keep every artifact of a run together.
const RESULTS_DIR = __ENV.RESULTS_DIR || ".";

const audioFile = open(AUDIO_FILE_PATH, "b");

export const options = {
  scenarios: {
    upload_audio: {
      executor: "per-vu-iterations",
      vus: VUS,
      iterations: 1,
      // Generous ceiling so k6 never cuts a VU off before its own MAX_WAIT_MS
      // polling loop gives up on its own.
      maxDuration: `${Math.ceil(MAX_WAIT_MS / 1000) + 60}s`,
    },
  },
  // Without this, k6 exits 0 even when checks fail — a collapse under load
  // (upload rejected, or the pipeline never reaching Completed) would only
  // show up as a note in the summary, not as a script failure.
  thresholds: {
    checks: ["rate==1.0"],
  },
};

// Time from the upload response to `overallStatus === "Completed"` — the
// number we actually care about comparing across configurations.
const pipelineTotalTime = new Trend("pipeline_total_time", true);
// Time for the upload POST itself, kept separate so "slow to accept the
// audio" doesn't get confused with "slow to process it".
const uploadResponseTime = new Trend("upload_response_time", true);
// Per-stage durations, read from the same final status response that closes
// the iteration — reuses the Stopwatch measurements the backend already
// persists (AudioRecord.CompressionDurationMs/TranscriptionDurationMs/SummaryDurationMs),
// not a new client-side measurement.
const compressionDuration = new Trend("compression_duration", true);
const transcriptionDuration = new Trend("transcription_duration", true);
const summaryDuration = new Trend("summary_duration", true);

export default function () {
  sleep(Math.random() * 2);
  
  const pipelineStart = Date.now();

  const uploadRes = http.post(`${BASE_URL}/api/audio/upload`, {
    file: http.file(audioFile, "sample.mp3", "audio/mpeg"),
  });

  uploadResponseTime.add(uploadRes.timings.duration);

  const uploadOk = check(uploadRes, {
    "upload accepted (202)": (r) => r.status === 202,
  });

  if (!uploadOk) {
    console.error("Upload failed");
    console.error(`Status: ${uploadRes.status}`);
    console.error(`Body: ${uploadRes.body}`);
    console.error(`URL: ${BASE_URL}/api/audio/upload`);
    return;
  }

  const audioId = uploadRes.json("id");

  let completed = false;
  while (Date.now() - pipelineStart < MAX_WAIT_MS) {
    sleep(POLL_INTERVAL_MS / 1000);

    const statusRes = http.get(`${BASE_URL}/api/audio/${audioId}`);
    if (
      statusRes.status === 200 &&
      statusRes.json("overallStatus") === "Completed"
    ) {
      completed = true;

      // null when a stage was never attempted (e.g. Summary after a failed
      // Transcription) — skip the Trend entirely rather than record a 0.
      const compressionDurationMs = statusRes.json("compressionDurationMs");
      const transcriptionDurationMs = statusRes.json("transcriptionDurationMs");
      const summaryDurationMs = statusRes.json("summaryDurationMs");

      if (compressionDurationMs !== null)
        compressionDuration.add(compressionDurationMs);
      if (transcriptionDurationMs !== null)
        transcriptionDuration.add(transcriptionDurationMs);
      if (summaryDurationMs !== null) summaryDuration.add(summaryDurationMs);

      break;
    }
  }

  pipelineTotalTime.add(Date.now() - pipelineStart);

  // Explicit failure signal: a collapse under load must show up as a failed
  // check, not just as a large (but "successful") pipeline_total_time.
  check(null, {
    [`pipeline completed within MAX_WAIT_MS (${MAX_WAIT_MS} ms)`]: () =>
      completed,
  });
}

// Defining handleSummary() makes k6 ignore the --summary-export flag, so
// the JSON export from run-benchmark.sh has to be reproduced here too —
// alongside the usual stdout text summary and the new HTML report.
export function handleSummary(data) {
  return {
    stdout: textSummary(data, { indent: " ", enableColors: false }),
    [`${RESULTS_DIR}/k6-summary.json`]: JSON.stringify(data),
    [`${RESULTS_DIR}/summary.html`]: htmlReport(data),
  };
}

import http from "k6/http";
import { check, sleep } from "k6";
import { Trend } from "k6/metrics";

// Each VU simulates one person uploading one audio and waiting for the full
// pipeline (Compression + Transcription->Summary) to reach "Completed".

const BASE_URL = __ENV.BASE_URL || "http://localhost:8080";
const AUDIO_FILE_PATH = __ENV.AUDIO_FILE_PATH || "./test-audio/sample.mp3";
const POLL_INTERVAL_MS = parseInt(__ENV.POLL_INTERVAL_MS || "1000", 10);
// The pipeline runs ~8-9s under normal conditions for the default test audio;
// this default leaves generous headroom for slower runs under load.
const MAX_WAIT_MS = parseInt(__ENV.MAX_WAIT_MS || "120000", 10);
const VUS = parseInt(__ENV.VUS || "1", 10);

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

export default function () {
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
      break;
    }
  }

  pipelineTotalTime.add(Date.now() - pipelineStart);

  // Explicit failure signal: a collapse under load must show up as a failed
  // check, not just as a large (but "successful") pipeline_total_time.
  check(null, {
    "pipeline completed within MAX_WAIT_MS": () => completed,
  });
}

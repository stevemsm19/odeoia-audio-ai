#import "common/lib/contentBox.typ": contentBox
#import "common/lib/slideLayouts.typ": theme, mainTitle, layoutA, layoutATwoCols, layoutATwoColsWithTitle, layoutBTwoCols, layoutC, layoutCTwoColsWithTitle, layoutDTwoCols

#let stepArrow = align(center, text(size: 22pt, fill: theme.primary, weight: "bold")[→])
#let downArrow = align(center, text(size: 16pt, fill: theme.primary, weight: "bold")[↓])

#let stageBadge(label) = contentBox(
  fill: theme.primary,
  text(fill: white, size: 16pt, weight: "bold", label),
)

#let codeBlock(body) = block(
  width: 100%,
  fill: luma(245),
  inset: 12pt,
  radius: 4pt,
  text(size: 14pt, font: "Menlo", body),
)

// ============================================================
// 1. Cover
// ============================================================
#mainTitle(
  title: "OdeoIA",
  subtitle: "Oscar Steven Muñoz Fajardo",
  content: [
    #v(160pt)

    #contentBox(
      fill: luma(150, 30%),
      text(
        fill: white,
        size: 18pt,
        [
          From a recorded meeting to useful information —
          automating audio processing
          and improving its performance step by step.
        ],
      ),
    )
  ],
)

// ============================================================
// 2. Agenda
// ============================================================
#layoutC(
  title: "Agenda",
  content: [
    #v(20pt)
    #text(
      size: 26pt,
      [
        1. The problem and the solution

        2. System architecture

        3. Processing evolution

        4. Benchmarks and evidence

        5. Conclusion

        6. Demo
      ],
    )
  ],
)

// ============================================================
// 3. The problem
// ============================================================
#layoutC(
  title: "The problem",
  content: [
    #align(center + horizon, [
      #text(
        size: 26pt,
        [
          Recording a work meeting is easy.

          #v(15pt)
          But after the meeting, there is still important work:
          understanding what was said and what was agreed.

          #v(25pt)
          #contentBox(
            fill: theme.primaryLightest,
            text(fill: white, size: 24pt, weight: "bold")[
              Reviewing 45 minutes of audio to find the
              important points takes time.
            ],
          )
        ],
      )
    ])
  ],
)

// ============================================================
// 4. The need
// ============================================================
#layoutC(
  title: "The need",
  content: [
    #align(center + horizon, [
      #grid(
        columns: (1fr, 1fr),
        column-gutter: 50pt,
        [
          #align(center)[
            #text(size: 25pt, weight: "bold", fill: theme.primary)[
              Who does it affect?
            ]
            #v(15pt)
            #text(size: 21pt)[
              People and teams that need to review
              recorded meetings without wasting time.
            ]
          ]
        ],
        [
          #align(center)[
            #text(size: 25pt, weight: "bold", fill: theme.primary)[
              What is lost today?
            ]
            #v(15pt)
            #text(size: 21pt)[
              Time and important decisions can be lost
              when nobody has time to review the audio.
            ]
          ]
        ],
      )
    ])
  ],
)

// ============================================================
// 5. The solution: OdeoIA
// ============================================================
#layoutC(
  title: "The solution: OdeoIA",
  content: [
    #align(center + horizon, [
      #contentBox(
        fill: theme.primaryLightest,
        text(fill: white, size: 26pt, weight: "bold")[
          You upload the meeting audio and OdeoIA takes care of the rest:
          it reduces the noise, creates the transcription, creates a summary
          and leaves the audio ready to play.
        ],
      )
    ])
  ],
)

// ============================================================
// 6. Technical introduction
// ============================================================
#layoutC(
  title: "What is it built with?",
  content: [
    #v(10pt)
    #grid(
      columns: (1fr, 1fr),
      column-gutter: 50pt,
      [
        #text(size: 23pt, weight: "bold", fill: theme.primary)[
          Main technologies
        ]
        #v(10pt)
        #text(size: 19pt)[
          - Backend: .NET 8
          - Frontend: React + Vite + TypeScript
          - Database: PostgreSQL
          - Files: Supabase Storage
          - Real-time status updates
        ]
      ],
      [
        #text(size: 23pt, weight: "bold", fill: theme.primary)[
          Processing
        ]
        #v(10pt)
        #text(size: 19pt)[
          - Audio: FFmpeg
          - Noise filter: FFmpeg
          - Transcription: whisper.cpp (Whisper tiny.en)
          - Summary: llama.cpp (Qwen2.5 0.5B Instruct)
          - Initial audio validation in the browser
        ]
      ],
    )
  ],
)

// ============================================================
// 7. Architecture — Context
// ============================================================
#layoutC(
  title: "Architecture — Context",
  content: [
    #align(center + horizon, [
      #image("./common/images/c4_context.png", width: 78%)
    ])
  ],
)

// ============================================================
// 8. Architecture — Components
// ============================================================
#layoutC(
  title: "Architecture — Components",
  content: [
    #align(center, [
      #grid(
        columns: (100%,),
        row-gutter: 10pt,
        contentBox(fill: theme.primary, inset: 8pt, text(fill: white, size: 16pt, weight: "bold")[
          AudioController — receives the audio and controls the process
        ]),
        downArrow,
        grid(
          columns: (1fr, 1fr),
          column-gutter: 20pt,
          contentBox(fill: theme.primaryLight, inset: 8pt, text(fill: white, size: 14pt, weight: "bold")[
            AudioProcessingLauncher \
            Divides the audio processing
          ]),
          contentBox(fill: theme.primaryLight, inset: 8pt, text(fill: white, size: 14pt, weight: "bold")[
            AudioBackgroundProcessingService \
            Controls how many audios can be active
          ]),
        ),
        downArrow,
        grid(
          columns: (1fr, 1fr),
          column-gutter: 20pt,
          contentBox(fill: theme.primaryLightest, inset: 8pt, text(fill: white, size: 14pt, weight: "bold")[
            Semaphore per audio \
            Maximum 3 active audios
          ]),
          contentBox(fill: theme.primaryLightest, inset: 8pt, text(fill: white, size: 14pt, weight: "bold")[
            Global Semaphore \
            Maximum 4 heavy processes
          ]),
        ),
        downArrow,
        grid(
          columns: (1fr, 1fr, 1fr),
          column-gutter: 16pt,
          contentBox(fill: luma(90), inset: 7pt, text(fill: white, size: 13pt)[FFmpeg]),
          contentBox(fill: luma(90), inset: 7pt, text(fill: white, size: 13pt)[whisper.cpp \ #text(size: 10pt)[Whisper tiny.en]]),
          contentBox(fill: luma(90), inset: 7pt, text(fill: white, size: 13pt)[llama.cpp \ #text(size: 10pt)[Qwen2.5 0.5B Instruct]]),
        ),
        downArrow,
        grid(
          columns: (1fr, 1fr, 1fr),
          column-gutter: 16pt,
          contentBox(fill: luma(90), inset: 7pt, text(fill: white, size: 13pt)[Postgres]),
          contentBox(fill: luma(90), inset: 7pt, text(fill: white, size: 13pt)[Storage]),
          contentBox(fill: luma(90), inset: 7pt, text(fill: white, size: 13pt)[SignalR Hub]),
        ),
      )
    ])
  ],
)

// ============================================================
// 9. Processing pipeline
// ============================================================
#layoutC(
  title: "Processing pipeline",
  content: [
    #align(center, [
      #grid(
        columns: (100%,),
        row-gutter: 8pt,
        contentBox(fill: theme.primary, inset: 7pt, text(fill: white, size: 15pt, weight: "bold")[
          Client uploads the audio
        ]),
        downArrow,
        grid(
          columns: (1fr, 1fr),
          column-gutter: 40pt,
          row-gutter: 5pt,
          align(center)[#text(size: 14pt, weight: "bold", fill: theme.primary)[Flow 1 — Audio processing]],
          align(center)[#text(size: 14pt, weight: "bold", fill: theme.primary)[Flow 2 — Transcription and summary]],
          contentBox(fill: theme.primaryLight, inset: 7pt, text(fill: white, size: 13pt)[Compression (FFmpeg)]),
          contentBox(fill: theme.primaryLight, inset: 7pt, text(fill: white, size: 13pt)[Transcription (whisper.cpp)]),
          downArrow, downArrow,
          contentBox(fill: theme.primaryLight, inset: 7pt, text(fill: white, size: 13pt)[Noise filter (afftdn)]),
          contentBox(fill: theme.primaryLight, inset: 7pt, text(fill: white, size: 13pt)[Summary (llama.cpp)]),
        ),
        text(size: 13pt, style: "italic")[These tasks can make progress at the same time],
        text(size: 12pt, style: "italic")[
          The global Semaphore limits heavy processes, even when several audios are active.
        ],
        downArrow,
        contentBox(fill: theme.primaryLightest, inset: 7pt, text(fill: white, size: 15pt, weight: "bold")[
          The results are saved and the status is updated in real time
        ]),
      )
    ])
  ],
)

// ============================================================
// 10. Bridge to the evolution
// ============================================================
#layoutC(
  title: "How the solution evolved",
  content: [
    #v(14pt)
    #align(center + horizon, [
      #grid(
        columns: (1fr, 0.22fr, 1fr, 0.22fr, 1fr, 0.22fr, 1fr, 0.22fr, 1fr),
        column-gutter: 5pt,
        align: horizon,
        contentBox(fill: theme.primaryDarkest, inset: 8pt, text(fill: white, size: 12.5pt, weight: "bold")[First version \ + Threads]),
        stepArrow,
        contentBox(fill: theme.primaryDark, inset: 8pt, text(fill: white, size: 12.5pt, weight: "bold")[TPL \ (Task.WhenAll)]),
        stepArrow,
        contentBox(fill: theme.primary, inset: 8pt, text(fill: white, size: 12.5pt, weight: "bold")[Semaphore \ (processes)]),
        stepArrow,
        contentBox(fill: theme.primaryLight, inset: 8pt, text(fill: white, size: 12.5pt, weight: "bold")[Parallel. \ ForEachAsync]),
        stepArrow,
        contentBox(fill: theme.primaryLightest, inset: 8pt, text(fill: white, size: 12.5pt, weight: "bold")[Semaphore \ final (audios)]),
      )

      #v(30pt)
      #text(size: 20pt)[
        Each stage appeared to solve a problem I found
        in the previous one — including `Parallel.ForEachAsync` itself,
        which was eventually replaced in the final version.
      ]
    ])
  ],
)

// ============================================================
// 11. Stage 1 — First version with Threads
// ============================================================
#layoutCTwoColsWithTitle(
  title: "Stage 1 — First version with Threads",
  contentLeft: [
    #stageBadge[First version]
    #v(14pt)
    #text(size: 18pt)[
      The first version used one `Thread` for each flow:
      one for audio processing and another for
      transcription and summary.

      #v(10pt)
      *Problem:* there was no clear control over how much work
      could run at the same time.
    ]
  ],
  contentRight: [
    #codeBlock[
      ```
      var thread = new Thread(() =>
      {
          // Runs one of the processing flows.
          // 
          invoke(handler, audioRecordId,
              correlationId)
              .GetAwaiter().GetResult();
      });

      thread.Start();
      ```
    ]
  ],
)

// 12. Stage 2 — TPL
// ============================================================
#layoutCTwoColsWithTitle(
  title: "Stage 2 — Split the flow",
  contentLeft: [
    #stageBadge[ThreadPool / TPL]
    #v(14pt)
    #text(size: 18pt)[
      I realized that audio processing and
      transcription did not directly depend on each other,
      so I separated them into two tasks that could move
      at the same time.

      #v(10pt)
      *Improvement:* I no longer had to manage Threads directly,
      and I could make better use of independent tasks.
    ]
  ],
  contentRight: [
    #codeBlock[
      ```
      Task compressionTask = Task.Run(() =>
          RunFlowAsync<ProcessAudioCompression>(
              "Compression", audioRecordId, ...));

      Task summaryTask = Task.Run(() =>
          RunFlowAsync<ProcessAudioSummary>(
              "Transcription+Summary", id, ...));

      await Task.WhenAll(
          compressionTask, summaryTask);
      ```
    ]
  ],
)

// 13. Stage 3 — Semaphore
// ============================================================
#layoutCTwoColsWithTitle(
  title: "Stage 3 — Control concurrency",
  contentLeft: [
    #stageBadge[Semaphore]
    #v(14pt)
    #text(size: 18pt)[
      After allowing more tasks at the same time, another
      problem appeared: several heavy operations could run
      together and use too many resources.

      #v(10pt)
      I used a global `SemaphoreSlim` to limit how many
      heavy operations can run at the same time.
      In the final configuration, the limit is 4.
    ]
  ],
  contentRight: [
    #codeBlock[
      ```
      await _externalProcessSemaphore
          .WaitAsync(cancellationToken);

      try
      {
          // Heavy process:
          // FFmpeg / whisper / llama
      }
      finally
      {
          _externalProcessSemaphore.Release();
      }
      ```
    ]
  ],
)

// 14. Stage 4 — Semaphore + Parallel.ForEachAsync
// ============================================================
#layoutCTwoColsWithTitle(
  title: "Stage 4 — Parallel.ForEachAsync",
  contentLeft: [
    #stageBadge[Parallel / Parallel programming]
    #v(14pt)
    #text(size: 18pt)[
      There was still another problem to solve.
      I not only wanted to process parts of one audio at the same time,
      I also wanted to receive several audios without losing control.

      #v(10pt)
      I used `Parallel.ForEachAsync` to allow up to 3 audios
      at the same time, taking items from the queue in groups
      of up to three.
    ]
  ],
  contentRight: [
    #codeBlock[
      ```
      var options = new ParallelOptions
      {
          MaxDegreeOfParallelism = maxDop,
          CancellationToken = stoppingToken,
      };

      await Parallel.ForEachAsync(
          claimedIds, options,
          async (audioId, ct) =>
      {
          await launcher.ProcessAsync(
              audioId, ..., ct);
      });
      ```
    ]
  ],
)

// 15. Stage 4 vs. 5 — comparison diagram
// ============================================================
#layoutCTwoColsWithTitle(
title: "Batches vs. slots: the key difference",
contentLeft: [
#align(center)[
#text(size: 15pt, weight: "bold", fill: theme.primary)[Parallel.ForEachAsync — by batches]
]
#v(8pt)
#grid(
columns: (100%,),
row-gutter: 6pt,
grid(
columns: (1fr, 1fr, 1fr),
column-gutter: 6pt,
contentBox(fill: theme.primaryDark, inset: 6pt, text(fill: white, size: 11pt)[Audio 1]),
contentBox(fill: theme.primaryLightest, inset: 6pt, text(fill: white, size: 11pt)[Audio 2 ✓]),
contentBox(fill: theme.primaryDark, inset: 6pt, text(fill: white, size: 11pt)[Audio 3]),
),
text(size: 11pt, style: "italic")[Audio 2 finishes, but its place remains unused],
downArrow,
contentBox(fill: luma(230), inset: 6pt, text(size: 12pt)[Wait for 1 and 3 to also finish]),
downArrow,
contentBox(fill: theme.primaryDarkest, inset: 6pt, text(fill: white, size: 12pt, weight: "bold")[Only then does a new batch enter]),
)
],
contentRight: [
#align(center)[
#text(size: 15pt, weight: "bold", fill: theme.primary)[Final Semaphore — by slots]
]
#v(8pt)
#grid(
columns: (100%,),
row-gutter: 6pt,
grid(
columns: (1fr, 1fr, 1fr),
column-gutter: 6pt,
contentBox(fill: theme.primaryDark, inset: 6pt, text(fill: white, size: 11pt)[Slot 1: Audio 1]),
contentBox(fill: theme.primaryLightest, inset: 6pt, text(fill: white, size: 11pt)[Slot 2: Audio 2 ✓]),
contentBox(fill: theme.primaryDark, inset: 6pt, text(fill: white, size: 11pt)[Slot 3: Audio 3]),
),
text(size: 11pt, style: "italic")[Audio 2 frees its slot when it finishes],
downArrow,
contentBox(fill: theme.primaryLight, inset: 6pt, text(fill: white, size: 12pt)[Slot 2 is available immediately]),
downArrow,
contentBox(fill: theme.primaryLightest, inset: 6pt, text(fill: white, size: 12pt, weight: "bold")[Audio 4 enters immediately — without waiting for 1 or 3]),
)
],
)

// 16. Stage 5 — Final architecture
// ============================================================
#layoutCTwoColsWithTitle(
  title: "Stage 5 — Final architecture: Semaphore per audio",
  contentLeft: [
    #stageBadge[Final architecture]
    #v(14pt)
    #text(size: 15pt)[
      I replaced `Parallel.ForEachAsync` with a `SemaphoreSlim`
      with individual slots for audios. The current limit
      is 3 active audios.

      #v(5pt)
      This control does not replace the global Semaphore: they work at
      different levels. The audio one controls how many audios are
      active, and the global one limits heavy processes to 4.

      #v(5pt)
      *This is the version currently running in production.*
    ]
  ],
  contentRight: [
    #codeBlock[
      ```
      foreach (var audioId in claimedIds)
      {
          // Slot for the complete audio.
          await _slots.WaitAsync(
              stoppingToken);

          Dispatch(audioId);
      }

      // Inside each flow:
      // Global Semaphore = maximum 4
      // heavy processes.
      ```
    ]
  ],
)

// 17. Benchmarks — measured evolution
// ============================================================
#layoutC(
  title: "Benchmarks — did it really improve?",
  content: [
    #align(center, [
      #image("../images/lb_9_bench_pipeline_evolution.png", height: 3.7in)
    ])
    #v(6pt)
    #align(center, [
      #contentBox(
        fill: theme.primaryLightest,
        inset: 10pt,
        text(fill: white, size: 17pt, weight: "bold")[
          At 20 virtual users: 179s in the first version → 70s
          in the final version. An important result: TPL + Semaphore
          without Parallel reached 196s, even worse than the first version.
        ],
      )
    ])
  ],
)

// ============================================================
// 18. Benchmarks — resource usage per architecture
// ============================================================
#layoutC(
  title: "Benchmarks — resource usage per architecture",
  content: [
    #align(center, [
      #image("../images/lb_9_bench_cpu_vu20.png", height: 3.6in)
    ])
    #v(6pt)
    #align(center, [
      #contentBox(
        fill: theme.primaryLightest,
        inset: 10pt,
        text(fill: white, size: 16pt, weight: "bold")[
          TPL + Semaphore without real parallelism used the most CPU (556%)
          despite being one of the slowest configurations — using more CPU
          does not automatically mean processing faster.
        ],
      )
    ])
  ],
)

// ============================================================
// 19. Benchmarks — duration per stage, per architecture
// ============================================================
#layoutC(
  title: "How long does each stage take, per architecture?",
  content: [
    #v(6pt)
    #align(center, [
      #table(
        columns: (1.9fr, 1fr, 1fr, 1fr, 1fr),
        align: (left, center, center, center, center),
        stroke: 0.6pt + luma(200),
        inset: 9pt,
        text(size: 12pt, weight: "bold")[Architecture (20 VU)], text(size: 12pt, weight: "bold")[Compression], text(size: 12pt, weight: "bold")[Transcription], text(size: 12pt, weight: "bold")[Summary], text(size: 12pt, weight: "bold")[Pipeline avg],
        text(size: 11.5pt)[Sequential], text(size: 11.5pt)[2.1 s], text(size: 11.5pt)[5.2 s], text(size: 11.5pt)[9.6 s], text(size: 11.5pt)[179.1 s],
        text(size: 11.5pt)[TPL + Semaphore(3) + Parallel.ForEachAsync(3)], text(size: 11.5pt)[2.1 s], text(size: 11.5pt)[5.0 s], text(size: 11.5pt)[10.5 s], text(size: 11.5pt)[126.3 s],
        text(size: 11.5pt, weight: "bold")[Final Semaphore (audios)], text(size: 11.5pt, weight: "bold")[1.5 s], text(size: 11.5pt, weight: "bold")[4.2 s], text(size: 11.5pt, weight: "bold")[8.9 s], text(size: 11.5pt, weight: "bold")[68.4 s],
      )
    ])
    #v(8pt)
    #align(center, [
      #text(size: 11.5pt, style: "italic")[
        Stage duration was not captured for the TPL + Semaphore run — that
        benchmark predates per-stage metric collection in the script, so only
        its total pipeline time is available.
      ]
    ])
    #v(10pt)
    #align(center, [
      #contentBox(
        fill: theme.primaryLightest,
        inset: 10pt,
        text(fill: white, size: 15.5pt, weight: "bold")[
          Each stage takes a similar amount of time across architectures
          (a few seconds each) — the big difference in total pipeline time
          does not come from the stages themselves, it comes from how long
          an audio waits before it is even admitted for processing.
        ],
      )
    ]),
    #v(4pt)
    #align(center, [
      #image("../images/lb_9_bench_completion_vu20.png", width: 60%)
        #text(size: 12pt)[Tasks completed at 20 VU]
    ])
    #v(6pt)
    #align(center, [
      #text(size: 10.5pt, style: "italic")[
        Each k6 virtual user waits at most 4 minutes (240s) for its audio to reach `Completed` —
        that is why the sequential version only shows 55% completion at 20 VU: those audios did
        not fail, they simply did not finish within that window. \
      ]
    ])
  ],
)

// ============================================================
// 21. Why this final architecture?
// ============================================================
#layoutC(
  title: "Why this final architecture?",
  content: [
    #v(6pt)
    #align(center, [
      #table(
        columns: (1.3fr, 1.6fr, 1.6fr),
        align: (left, left, left),
        stroke: 0.6pt + luma(200),
        inset: 7pt,
        text(size: 12pt, weight: "bold")[Stage], text(size: 12pt, weight: "bold")[Solved], text(size: 12pt, weight: "bold")[Limitation],
        text(size: 11.5pt)[Threads], text(size: 11.5pt)[Basic concurrency], text(size: 11.5pt)[No control over how many threads are created],
        text(size: 11.5pt)[TPL], text(size: 11.5pt)[Real parallelism within one audio], text(size: 11.5pt)[Does not limit how many audios run at once],
        text(size: 11.5pt)[Semaphore], text(size: 11.5pt)[Protects CPU/RAM from FFmpeg/whisper/llama], text(size: 11.5pt)[Does not limit how many complete audios are processed],
        text(size: 11.5pt)[Parallel.ForEachAsync], text(size: 11.5pt)[Several audios at the same time], text(size: 11.5pt)[By batches — a slow audio blocks free slots],
        text(size: 11.5pt, weight: "bold")[Semaphore final], text(size: 11.5pt, weight: "bold")[Controls audios by slots], text(size: 11.5pt)[Does not remove the global process limit],
      )
    ])
    #v(16pt)
    #align(center, [
      #contentBox(
        fill: theme.primaryLightest,
        inset: 10pt,
        text(fill: white, size: 15.5pt, weight: "bold")[
          I did not choose it because it was theoretically better: with the same 100%
          completion rate, it reduced total time by 33.7% and lowered CPU use.
          Profiling also showed that it was important to control
          audio admission without depending on batches.
        ],
      )
    ])
  ],
)

// ============================================================
// 22. Conclusion
// ============================================================
#layoutC(
  title: "Conclusion",
  content: [
    #v(10pt)
    #text(size: 18pt)[
      - *Initial problem:* processing did not respond well when the load increased.
      - *Evolution:* Threads → TPL → Semaphore → Parallel.ForEachAsync →
        Semaphore final por audio.
      - *Applied concepts:* Threads, tasks, async/await, ThreadPool,
        Semaphore and Parallel — including why `Parallel` was replaced.
      - *Result:* \~50% less time under load (179s → 70s at 20 VU)
        and from 55% to 100% of completed tasks.
      - *Final approach:* Semaphore per audio — same success rate as
        Parallel.ForEachAsync, but 33.7% faster and with less CPU.
      - *Key lesson:* doing more things at the same time does not always
        mean doing it faster, and not every parallelism mechanism
        behaves the same under uneven load.
    ]
  ],
)

// ============================================================
// 23. Demo
// ============================================================
#layoutA(
  content: [
    #align(center + horizon, [
      #text(size: 50pt, weight: "bold", fill: theme.primary)[
        Demo
      ]

      #v(25pt)

      #text(size: 22pt)[
        OdeoIA in action
      ]
    ])
  ],
)

// ============================================================
// Backup slides (not part of the main presentation)
// ============================================================

#layoutC(
  title: "Backup — Simplified request sequence",
  content: [
    #v(10pt)
    #text(size: 18pt)[
      1. Client makes `POST /api/audio/upload` with the file.
      2. The controller saves the original audio and marks it `Pending`.
      3. `AudioProcessingLauncher` downloads the audio once and starts
         Compression and Transcription→Summary as independent `Task`s.
      4. Each flow acquires the global `SemaphoreSlim` before calling
         FFmpeg / whisper.cpp / llama.cpp, with a maximum of 4
         heavy processes at the same time, and releases it when finished.
      5. Audio processing uses a maximum of 3 slots; an audio
         can occupy a slot while waiting for the global limit to be available.
      6. Each stage saves its result and updates the status.
      7. SignalR notifies the client in real time — without polling.
      8. The client plays the filtered audio and reads the summary.

    ]
  ],
)

#layoutC(
  title: "Backup — Next steps",
  content: [
    #v(10pt)
    #text(size: 20pt)[
      - Consolidate benchmark results into a single CSV
        for historical analysis between variants.
      - Live metrics dashboard (duration per stage, CPU,
        success rate) instead of reports per run.
      - Explore moving the noise filter before compression
        to evaluate its impact on perceived quality.
      - Auto-scale `MaxDegreeOfParallelism` based on available CPU
        instead of a fixed value per environment.
    ]
  ],
)

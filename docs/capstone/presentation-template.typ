#import "common/lib/contentBox.typ": contentBox
#import "common/lib/slideLayouts.typ": theme, mainTitle, layoutA, layoutATwoCols, layoutATwoColsWithTitle, layoutBTwoCols, layoutC, layoutCTwoColsWithTitle, layoutDTwoCols

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
          Audio compression, transcription, and summarization
          using concurrency, synchronization, and asynchronous programming.
        ],
      ),
    )
  ],
)

#layoutC(
  title: "Agenda",
  content: [
    #v(20pt)

    #text(
      size: 28pt,
      [
        1. Problem, Scope and Objective

        2. System Architecture

        3. Execution Pipeline

        4. Concurrency and Synchronization

        5. Asynchronous Programming

        6. Data Flow and Persistence

        7. Demo
      ]
    )
  ]
)

#layoutC(
  title: "Problem, Scope and Objective",
  content: [
    #align(center + horizon, [
      #text(
        size: 27pt,
        [
          Process audio files without keeping
          the HTTP request waiting for heavy operations. (FFmpeg, Whisper, Llama --- qwen2.5 model)

          #v(25pt)

          #strong[Scope]

          MP3 / WAV → Compression + Transcription → Summary

          #v(25pt)

          #strong[Objective]

          Apply concurrency, synchronization, and asynchronous
          programming where they are actually needed.
        ]
      )
    ])
  ]
)

#layoutC(
  title: "Architecture — System Context",
  content: [
    #align(center + horizon, [
      #image("./common/images/c4_context.png", width: 80%)
    ])

    #link("https://www.plantuml.com/plantuml/png/VP91RXD144NtSuh2AqSCP9CbGh66H215YXM7dIMhqbso6tLtZRggZMV78RW6f-0cd8GPjvb9a9LTwLVx_VB_SosAINFmS6-9loms3KUbWrRL6nUDpvRWGtDrsLpzMtoiAdNgwVGU_lpu2QSLWoKFuaGfO5KjAGd7UPQCoN4DiwzbclNZAZyHv0YOhMFem72Kx1Kl4xM9RJO4v27HKRGykwYgLJTmvoTypoW73BI6GvjSe1eqOHIJN4_lZZyF19ZCpkrPm5BncsYRcuFETvGI1aTHKUxOe2VfcP-VgU2BF7ZTteQMjZLytpbfARqpta47tcF0OLv75_TiqBA0uS3G9ZOaHtSFgtlO__w5C9pOgi2oqnt7_-wHtE8J2XrZAYCqdhCTx9Sikaqai2LHnn4jGui98P2YHMLPnxUmKawufR8f6NDoMnVHrx1ODD21v10mTU_re8F_0_dDmzJLbFBGL47KOqj0ChHIh-DKLmUUnj82nTcGuUFZSZNHH-Zvn6--aScABxPWQHrBX4NYiKKWwDi_mJzDy6VnBIaTvIaSwCRFuSM1TzYT-ViNHN_ryFBcZ7zFlQPeS_1_0G00")[
      #align(center + horizon)[
        #text(size: 10pt, weight: "bold")[
          View diagram
        ]
      ]
    ]
  ]
)

#layoutC(
  title: "Architecture — Containers",
  content: [
    #align(center + horizon, [
      #image("./common/images/c4_containers.png", width: 50%)
    ])

    #link("https://www.plantuml.com/plantuml/png/XLHTRXit47xNAQRkYuKuKjocY6iKHHHPHeqAhc2fpQCmueua9boIvOyZeIZGG_G2FK2VUWJVf2VfS9VYoj53zsMvGyutttpyPj_wW2x4Mi5F5PcxqN9iT42foVbUxmkfXOeLmNVZryFnwsxl-ruloA0e9y6_V_m9QPCqLSOHfyuOmEZpw2CwQIxWxETsTTRlzUPx7wXUlZDHL-ZsvrwcR-H3BTnP7txh0J-bthdUIBtZ0_VfVSQB9d1zEVZgpKjKbi-au5owHm9Ii8fILK0-FFqLf330BJmGYW1xsA8WiCxizakBtgUj8Lev1EGq51z9LypnUVMruoLlS8dRl8Jtj4ebMzZBR-45_283yMknjpGNJjhGK8eh0eoLDFu2XD4-ge10Af73YeLnn6IgA1BlzY2Jh2Dlk-OSgnbhqYUSCbSkCPhTmRlCd3_7Ny8G1lUJ1Lorzw88DOoSs38_4QAZbhUGHype0uaO49Jni5Rn0x-gHYP17cj9EYHQjBD64_YOsJEfXjKKzykRmnqTnEftMvs0MMBU_BtuOB8BvpvQl5ablNYnGaymCpvikFb4Te4hXItNfIDXNEMJbb0Jiy30PFUPaGTh773O8fFxhC0y68SREgwIGwd8IDKeIESwO9paVb1T02Rf7XbwD1fp8RwEcbqx2Bl09OvhhDUrfKqooMrQm7Xwbu1dZOg6rGlaj06B3fb-RRax-VIFJhZFoNxQIc_9SMvUlHHAmY4y4DRs4_2TVZI2csOTAeaiS9OeEDI-SQ0qUld9k8-SPFVFQoY5pSWrxnQ_MVulUkx_6MxlWTJoePlsR0KUUeHqSrP9WI8rcv1_M2ncSxZ-vlBDrpdtr3rbcelwPPez9xKvvN0fED6FJtyZUCC5POXLwvyr1R4zx-UyKgiReUv_qFuC3YpX-khLrQkkmP9H_3eb6b8IUqKmIleyDFvC09DR61j7C8Hxk_6_gcOQ5-FPS36TC-GfOcVGpep5Xgtxok_WsCGyyVER7nluHjfEr89Uh3bf1_t4VyMdVZ0Fw0AyWFSemwrnavqCdNOTP35fmIo-ApSzsuUjqOnrvEDJf6p5WjFrs04KcwRcqazb-fdRSlZaZlgduI9qpZdHwP3JVfLM3-7ycOapo5lEZRNw3m00")[
      #align(center + horizon)[
        #text(size: 10pt, weight: "bold")[
          View diagram
        ]
      ]
    ]
  ]
)

#layoutC(
  title: "Architecture — Components",
  content: [
    #align(center + horizon, [
      #image("./common/images/c4_components.png", width: 52%)
    ])

    #link("https://www.plantuml.com/plantuml/png/jLZDRjms4BxlKqnzgGq43b3arXz4aMLNmEvg8Sb8SrhGr7ZDW28LaagzAGhqr0Se-W8vzj1J7y5lqYSfkQ8iPRNQ6DsDBwRcPuRyzCqCjI-r8ShK9OUaG1a7os0HBqDPLbAWC7eo-Oe9oki2uRlmnVFmHQVxOJ8np73qZl3lRt_2en8r5CYXKfAY9YMp8cabGEg2oSba6hn9hlDbdYoM5ydh-UdPPF8rVFz__woltVL14IQX58OmWMhvIjQY86fzIYhs34u2j-F5vXnQglEWOYTdyCi40BhJdbBhhIJdg5gFy55ouaJPudmUvV2fD4NARX2GDx0y0nj6rzmGG6r88G612KOPWMqeJyuclnuAMJo_J8CiJw_3_3gDcjUNpCFhD8tcuPjZe-aWehLIAEXwY6eiRXNHHjNKr0ezbLfxHdOWpaajw1sghLXChAPUun2Ez-X2XGGAgI6_iwj20zvN_E5loepKtm8b1O5QIA3EJDSE_uf9q5HMw9Xy4SCQy2tIsZX3kENrswEybs2ncCPXaCV9_0jGkkAC4iEas47jJhbv0ubwLpiYKlRmZzX9-h9I3bSfVYIYQCWVTaBmKWUspzAKHcTYbwKrwXcsIG5v0wd3scTgzh6r2rtR46hTxI1h16FPlNfFvbmHeQbYbJkuXUBtFo15cmQ7oIjKjrEvUiJIFafburAqKXSqQpbTW9MishfVCMqKWQ9Mf4co1eMRqY_SWHlseI4L-KAL7iyMqsWMpVCDpxAZ4usvWdvIdSTbnT5YwtWc1UeTH5DOoJPMQjUQ6QdMNNrtCdWEqQNrLEYqKqGBBB6v26SV27XZAbKnf95r9olSyASYDqHZraWoLEyPnIxRK7w1cXAr8YuPBmb5GO0eUiVUItkSyQhPAfT1ffwofujyp-6aNnSG1E4WbscPx0i-xpyFytNgqOoF4UcUOhHO-uHP_tcOiA_k9xn2WMhJ0jH8uMaiINKd5ReaxJhZh6pAdPNCiFV4bP9cWYefs0TVQOvp6HChZaOAPprx-7ZFIWdV-14htIGmg8HTLAY96n6QbROu7puAbFeO5Hdzb4VfF9Z28at2ACkY0oimMsk3vJAwDwUtjsM5hYDTNhf5r_vaUomeSCYSVeYVxvYkDhtqTRCwfrKr4cX0Y5uSpab9N1sw_tjYh51WsmWBxDxveIZFWdWEbzFazM7WfiYN5vzSyNgNZ-P6qTqmJbdXxd5A8T-mEYxEx45yZCvlv-niBYBGNAMRUmULrTPLep4Fz5hGKnjw47am3ttKLq-FUZZoOJ9Rf9RNSNBC5p24p4y6r-YVTutUNvyTEAEUlj_tkxTKRCK4uJQKhc_GryLuYDsTF9S1eKS3ChkUpOBqpT50xDZnT027NcDpvQPZq8Zds83ul6CtdzsxyUfpSs-ioz6xyP7v-WoQhjpDqiTpRa7PjjwXuHPolhTsPWU2qF_sS-iyJQP7Hw4_jOFYNSrGETvnInn5e49LCYr7e7cYyn2kfpiV3k8YIY-JT1RCmmYcoTLLFByw7CHqP_VndnxzpudouI-Z62NQev0EgcYVq-6dRtxLEUp0Kx8U6RPdNhbtYfuvcn74-c1quJxNsfk8d-c1FbZgFrLyisetuny7ORSsse_PMGpAe4zidtTdisdynpgCr-viAuzHlMslVApg9OgYBlb_")[
      #align(center + horizon)[
        #text(size: 10pt, weight: "bold")[
          View diagram
        ]
      ]
    ]
  ]
)

#layoutC(
  title: "Execution Pipeline",
  content: [
    #align(center + horizon, [
      #image("./common/images/pipeline.png", width: 62%)
    ])

    #link("https://mermaid.live/edit#pako:eNqtVl1u20YQvsqATxQiyxJtSy4BBxBlK02g2G5EoUCrPmzIsbT1LpdZLmMphoE-9QBFL9AD9KlH8E1yks5SpH5sxrCL6kWi9vtmZ79vdoa3TqRidHwnw085JhGecjbTTE4ToA_LjUpy-RH16jll2vCIpywxMAGWwSTLmeYK3KFWicEkbjwGDiywn8dcDQijlRC14QrYJBWKxQV4kuGAZfgYOVrHu9QqwizjyWzE8iSa18UN-xYezjWyGPrgDpRMtSWppCbXMNhCB-CGmiVZpHlqCP9qnEvJ9LKGN0ZpifTF0rnSOBZcgivu_5bc4IlXwxgOLWE4lCnOauKFoV2-mfMsRb0XCQ7ujdLXqOFyaea1uY9G7y1HCCbZ8xhjo4qs85R9JKnts2azGslPgx3cpcrMjEScJivoZO_164EPlxfjEPZZyveZdWc_L8wEV-bCcBuuTGFA8AnhzxYY5Qb72TKJ3MyQ5rIJV1zgOZPYgJ_ZDePml3ILS6L8_O0SWTGV5jOeMFHHOA18GLPPWMA_YKR0XJDAvaRipcJ5QLKckQ-rctqUl8vjJhBZo2C2Et5WdX6uDIL6TCITK8GbsnTcVqvVaI0NndltwMKDr7_9CW-zgEXXM63yJD4xOi91Hu3RnmHfL6kVa3sxqF8c2MWJD17bg34UYWqQ1LapZoaZPDupzlj5RB7AUOS_KuisLwK__6cqDfsJ-6Vq75m-7mcbCeAVKR8zOu6OYGtO4c2bsxAqO8D93ph0IDgmpvEtEkoffqQFUslKFIwufpic9c_Dsw2QMJVCLP6Uc81j9SDMcEjFt8p0pRAla6MOlT5bPBl8OKxitxiLYIYJahZTP0PiQbtRl-8HFEi3wG3UKbCqzlW0-jNvxLUOCCTT3KhsShhP9MM6ZklcmuZt9aPI-gZff_8DdptSsU2wtc1OB3uuocF_MTR4oaFBvaFFmDB82lHDJarcPLFJGFabmLUCQF0mJhq8G1-cN2F8MboATECipDOyRt1Z6sx-Qt61pd_Qp6KVpr3Ij_9HWpoSO5KkSlO7oDbRfJnAFKfaKlsdZkfdF4m5fXPKYC2zMM_VcHOPSvLjS0SNsGqCQqmUJpgQVvOIxQy8jGbU_V-LFnTaIPl2Oywnm70GW4Ptlsd3G0w1Z96gCZZvy_nyKPWqWdthwYQYFx2aPEfq1bHKCh_QsJQ9SniyfRm35XGv0ERziLnGiCZ5jAISmnUziqcbG-7ECp_EqPkXVr43CbZE7dom1aAUDC5KOvWgXGLiNJ0ZlY7j2ynVdCRqyeyjc2ujTh0zR4lTx6efMRkxdabJHXHoVeEnpWRFo0E3mzv-FRMZPeVFbZdvl-t_dZHYgEaicfxO77vDIorj3zoLeu50W72Dw6Pjbrvn9Q4O2rS6dPy9o8N2y_OOj7xep9c-aB9175rOl2Jjr3V81Dk4Pu72vM5R77Db7d79C6_ddV4")[
      #align(center + horizon)[
        #text(size: 10pt, weight: "bold")[
          View diagram
        ]
      ]
    ]
  ]
)

#layoutC(
  title: "Concurrency — Thread",
  content: [
    #align(center + horizon, [
      #contentBox(
        fill: theme.primaryLightest,
        text(
          size: 27pt,
          fill: white,
          [
            Two independent processing flows.

            #v(20pt)

            Thread A → Compression

            Thread B → Transcription → Summary
          ]
        )
      )

      #v(35pt)

      #text(
        size: 22pt,
        [
          Both flows can progress concurrently.
        ]
      )
    ])
  ]
)

#layoutC(
  title: "Synchronization — SemaphoreSlim",
  content: [
    #align(center + horizon, [
      #text(
        size: 27pt,
        [
          FFmpeg · Whisper · Llama
        ]
      )

      #v(35pt)

      #contentBox(
        fill: theme.primaryLightest,
        text(
          size: 26pt,
          fill: white,
          [
            Maximum of 2 heavy external
            processes running simultaneously.
          ]
        )
      )

      #v(35pt)

      #text(
        size: 21pt,
        [
          Thread enables concurrency.

          SemaphoreSlim controls how much concurrency I allow.
        ]
      )
    ])
  ]
)

#layoutC(
  title: "Asynchrony — async / await",
  content: [
    #align(center + horizon, [
      #grid(
        columns: (1fr, 1fr),
        column-gutter: 50pt,
        [
          #align(center)[
            #text(
              size: 25pt,
              weight: "bold",
              fill: theme.primary
            )[
              Asynchronous I/O
            ]

            #v(20pt)

            #text(size: 21pt)[
              Storage

              Database

              HTTP

              Streams
            ]
          ]
        ],
        [
          #align(center)[
            #text(
              size: 25pt,
              weight: "bold",
              fill: theme.primary
            )[
              Blocking
            ]

            #v(20pt)

            #text(size: 21pt)[
              FFmpeg

              Whisper

              Llama

              WaitForExit()
            ]
          ]
        ],
      )
    ])
  ]
)

#layoutC(
  title: "Data Flow and Persistence",
  content: [
    #align(center + horizon, [
      #text(
        size: 25pt,
        [
          Original Audio → Storage

          #v(20pt)

          Compressed AAC → Storage

          #v(20pt)

          Transcription → In Memory

          #v(20pt)

          Summary → Storage
        ]
      )
    ])
  ]
)

#layoutA(
  content: [
    #align(center + horizon, [
      #text(
        size: 50pt,
        weight: "bold",
        fill: theme.primary,
        [
          Demo
        ],
      )

      #v(25pt)

      #text(
        size: 22pt,
        [
          OdeoIA in action
        ]
      )
    ])
  ]
)
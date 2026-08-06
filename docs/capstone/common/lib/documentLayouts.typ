#let documentLayout(body) = {
  set page(
    paper: "a4",
    margin: (x: 2.5cm, y: 2.5cm),
    header: context {
      if counter(page).get().first() > 1 {
        set text(size: 10pt)
        grid(
          columns: (1fr, 1fr),
          align(left)[
            
          ],
          align(right)[
            #place(
              top + right,
              dx: 1cm,
              dy: -1cm,
              image("../images/brand.svg", height: 2cm)
            )
          ]
        )
      }
    },
    header-ascent: 1.3cm,
    footer: context {
      // Don't show footer/numbering on the first page
      if counter(page).get().first() > 1 {
        set text(size: 10pt)
        align(center)[#counter(page).display("1")]
      }
    },
  )

  set text(
    size: 11pt,
    font: "Arial",
    lang: "es", 
  )

  set par(
    justify: true,
    leading: 1.5em,
  )

  set heading(numbering: "1.1.")
  
  show heading: it => {
    v(1.5em, weak: true)
    it
    v(1em, weak: true)
  }

  body
}

#let coverPage(
  title: "Arquitectura de software 3 - CSAR-484.GA.T2.26.M1",
  professor: "Santiago Henn",
  student: "Oscar Steven Muñoz Fajardo",
  date: "Julio 12 del 2026",
  lab: "Laboratorio 1"
) = {
  align(center + horizon)[
    #block(
      width: 100%,
      [
        #v(2cm)

        #text(size: 30pt, weight: "bold")[#title]
        
        #v(5cm)
        
        #text(size: 16pt)[
          *Profesor:*
          #v(-0.1cm)
          #professor \
          #v(1cm)
          *Estudiante:* \
          #v(-0.1cm)
          #student
        ]
        
        #v(5cm)
        
        #text(size: 14pt)[
          *JALA UNIVERSITY* \
          #lab - #date
        ]
      ]
    )
  ]
  pagebreak()
}

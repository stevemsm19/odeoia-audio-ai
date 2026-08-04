import { Link } from "react-router-dom";
import "./AboutPage.css";
export function AboutPage() {
  return (
    <>
      <section className="about-hero">
        <span className="eyebrow">ABOUT ODEOIA</span>
        <h1>
          Better listening starts with <em>less noise.</em>
        </h1>
        <p>
          OdeoIA is a focused workspace for turning raw recordings into clear,
          useful information.
        </p>
      </section>
      <section className="about-grid">
        <div className="about-visual">
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
          <span>MAKE SPACE FOR WHAT MATTERS</span>
        </div>
        <div>
          <span className="eyebrow">OUR PURPOSE</span>
          <h2>Give every recorded conversation a second life.</h2>
          <p>
            Meetings, interviews, voice notes, and ideas often contain more
            value than time allows us to revisit. OdeoIA makes those moments
            easier to hear, understand, and act upon.
          </p>
          <Link className="inline-link" to="/signup">
            Start with OdeoIA <span>→</span>
          </Link>
        </div>
      </section>
    </>
  );
}

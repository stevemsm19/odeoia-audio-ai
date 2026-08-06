import { Brand } from "../Header/Header";
import "./Footer.css";
export function Footer() {
  return (
    <footer className="site-footer">
      <Brand />
      <p></p>
      <span>© {new Date().getFullYear()} OdeoIA</span>
    </footer>
  );
}

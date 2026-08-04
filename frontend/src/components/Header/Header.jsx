import { NavLink, Link } from "react-router-dom";
import { useState } from "react";
import { useAuth } from "../../features/auth/AuthContext";
import "./Header.css";

export function Brand() {
  return (
    <Link className="brand" to="/" aria-label="OdeoIA home">
      <span className="brand-mark">
        <i />
        <i />
        <i />
      </span>
      Odeo<span>IA</span>
    </Link>
  );
}
export function Header() {
  const [open, setOpen] = useState(false);
  const { user, logout } = useAuth();
  const close = () => setOpen(false);
  return (
    <header className="site-header">
      <div className="nav-shell">
        <Brand />
        <button
          className="menu-button"
          onClick={() => setOpen(!open)}
          aria-label="Toggle navigation"
          aria-expanded={open}
        >
          ☰
        </button>
        <nav className={open ? "navigation open" : "navigation"}>
          <NavLink onClick={close} to="/" end>
            Process audio
          </NavLink>
          <NavLink onClick={close} to="/about">
            About
          </NavLink>
          {user ? (
            <>
              <span className="user-greeting">Hi, {user.name}</span>
              <button
                className="text-button"
                onClick={() => {
                  logout();
                  close();
                }}
              >
                Log out
              </button>
            </>
          ) : (
            <>
              <NavLink onClick={close} to="/login">
                Log in
              </NavLink>
              <Link onClick={close} className="nav-cta" to="/signup">
                Get started
              </Link>
            </>
          )}
        </nav>
      </div>
    </header>
  );
}

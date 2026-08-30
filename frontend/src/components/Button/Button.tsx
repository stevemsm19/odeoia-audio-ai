import type { ButtonHTMLAttributes } from "react";
import "./Button.css";

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  loading?: boolean;
};

export function Button({
  children,
  className = "",
  loading = false,
  ...props
}: ButtonProps) {
  return (
    <button
      className={`button ${className}`}
      {...props}
      disabled={loading || props.disabled}
    >
      {loading && <span className="spinner" aria-hidden="true" />}
      {children}
    </button>
  );
}

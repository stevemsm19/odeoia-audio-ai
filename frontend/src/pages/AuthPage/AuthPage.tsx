import { useState, type ChangeEvent, type FormEvent, type InputHTMLAttributes } from "react";
import { Link, useNavigate } from "react-router-dom";
import { Button } from "../../components/Button/Button";
import { useAuth } from "../../features/auth/AuthContext";
import "./AuthPage.css";

const validateEmail = (email: string) => /^\S+@\S+\.\S+$/.test(email);

type AuthPageProps = { mode: "login" | "signup" };

type AuthValues = {
  name: string;
  email: string;
  password: string;
  confirmation: string;
};

type AuthErrors = Partial<Record<keyof AuthValues, string>>;

export function AuthPage({ mode }: AuthPageProps) {
  const signup = mode === "signup";
  const { login, signup: createAccount } = useAuth();
  const navigate = useNavigate();
  const [values, setValues] = useState<AuthValues>({
    name: "",
    email: "",
    password: "",
    confirmation: "",
  });
  const [errors, setErrors] = useState<AuthErrors>({});
  const [loading, setLoading] = useState(false);
  const update = (event: ChangeEvent<HTMLInputElement>) =>
    setValues((current) => ({
      ...current,
      [event.target.name]: event.target.value,
    }));
  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const next: AuthErrors = {};

    if (signup && !values.name.trim()) next.name = "Please enter your name.";
    if (!values.email) next.email = "Email is required.";
    else if (!validateEmail(values.email))
      next.email = "Enter a valid email address.";

    if (!values.password) next.password = "Password is required.";
    else if (values.password.length < 8)
      next.password = "Use at least 8 characters.";

    if (signup && values.confirmation !== values.password)
      next.confirmation = "Passwords do not match.";
    setErrors(next);

    if (Object.keys(next).length) return;
    setLoading(true);

    try {
      await (signup ? createAccount(values) : login(values));
      navigate("/");
    } finally {
      setLoading(false);
    }
  };
  return (
    <section className="auth-page">
      <div className="auth-copy">
        <h1>
          {signup
            ? "Turn recordings into clear next steps."
            : "Welcome back to OdeoIA."}
        </h1>
      </div>
      <div className="auth-card">
        <p className="eyebrow">
          {signup ? "CREATE YOUR ACCOUNT" : "SIGN IN TO ODEOIA"}
        </p>
        <h2>{signup ? "Start with OdeoIA" : "Log in"}</h2>
        <p className="muted">
          {signup
            ? "Create an account in seconds."
            : "Continue to your audio workspace."}
        </p>
        <form onSubmit={submit} noValidate>
          {signup && (
            <Field
              label="Full name"
              name="name"
              value={values.name}
              onChange={update}
              error={errors.name}
              autoComplete="name"
            />
          )}
          <Field
            label="Email address"
            name="email"
            type="email"
            value={values.email}
            onChange={update}
            error={errors.email}
            autoComplete="email"
          />
          <Field
            label="Password"
            name="password"
            type="password"
            value={values.password}
            onChange={update}
            error={errors.password}
            autoComplete={signup ? "new-password" : "current-password"}
          />
          {signup && (
            <Field
              label="Confirm password"
              name="confirmation"
              type="password"
              value={values.confirmation}
              onChange={update}
              error={errors.confirmation}
              autoComplete="new-password"
            />
          )}
          <Button loading={loading} type="submit" className="wide">
            {loading
              ? signup
                ? "Creating account…"
                : "Signing in…"
              : signup
                ? "Create account"
                : "Log in"}
          </Button>
        </form>
        <p className="form-switch">
          {signup ? "Already have an account?" : "New to OdeoIA?"}{" "}
          <Link to={signup ? "/login" : "/signup"}>
            {signup ? "Log in" : "Create an account"}
          </Link>
        </p>
      </div>
    </section>
  );
}

type FieldProps = { label: string; error?: string } & InputHTMLAttributes<HTMLInputElement>;

function Field({ label, error, ...props }: FieldProps) {
  const id = `field-${props.name}`;
  return (
    <label className="field" htmlFor={id}>
      {label}
      <input
        id={id}
        {...props}
        aria-invalid={Boolean(error)}
        aria-describedby={error ? `${id}-error` : undefined}
      />
      {error && (
        <span id={`${id}-error`} className="field-error" role="alert">
          {error}
        </span>
      )}
    </label>
  );
}

import { createContext, useContext, useMemo, useState, type ReactNode } from "react";
import * as authService from "../../services/authService";
import type { User } from "../../services/authService";

type AuthContextValue = {
  user: User | null;
  login: (values: { email: string; password?: string }) => Promise<void>;
  signup: (values: { name?: string; email: string; password?: string }) => Promise<void>;
  logout: () => void;
};

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(() => authService.currentUser());
  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      login: async (values) => {
        const next = await authService.login(values);
        setUser(next);
      },
      signup: async (values) => {
        const next = await authService.signup(values);
        setUser(next);
      },
      logout: () => {
        authService.logout();
        setUser(null);
      },
    }),
    [user],
  );
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

// Always used inside AuthProvider (App.tsx wraps the whole tree) — asserted
// non-null rather than adding a runtime guard the app never exercises.
export const useAuth = () => useContext(AuthContext)!;

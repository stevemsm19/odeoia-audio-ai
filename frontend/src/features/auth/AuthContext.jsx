import { createContext, useContext, useMemo, useState } from "react";
import * as authService from "../../services/authService";

const AuthContext = createContext(null);
export function AuthProvider({ children }) {
  const [user, setUser] = useState(() => authService.currentUser());
  const value = useMemo(
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
export const useAuth = () => useContext(AuthContext);

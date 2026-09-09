"use client";

import { createContext, useContext, useEffect, useMemo, useState } from "react";
import { api } from "./api";
import type { AuthUser, MemberRole } from "./types";

type AuthContextValue = {
  user: AuthUser | null;
  loading: boolean;
  isLead: boolean;
  isOfficeManagement: boolean;
  login: (email: string, password: string) => Promise<void>;
  logout: () => void;
};

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const token = window.localStorage.getItem("gg.token");
    if (!token) {
      setLoading(false);
      return;
    }
    api
      .me()
      .then((me) => setUser({ ...me, token }))
      .catch(() => {
        window.localStorage.removeItem("gg.token");
        setUser(null);
      })
      .finally(() => setLoading(false));
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      loading,
      isLead: user?.role === ("Lead" as MemberRole),
      isOfficeManagement: user?.isOfficeManagement === true,
      async login(email, password) {
        const response = await api.login(email, password);
        window.localStorage.setItem("gg.token", response.token);
        setUser(response);
      },
      logout() {
        window.localStorage.removeItem("gg.token");
        setUser(null);
      },
    }),
    [user, loading],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) throw new Error("useAuth must be used within AuthProvider");
  return context;
}

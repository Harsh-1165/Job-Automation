const TOKEN_KEY = "jobflow_access_token";
const EXPIRES_KEY = "jobflow_token_expires_at";

export interface AuthState {
  accessToken: string;
  expiresAt: string;
}

export function saveAuth(state: AuthState): void {
  if (typeof window === "undefined") return;
  localStorage.setItem(TOKEN_KEY, state.accessToken);
  localStorage.setItem(EXPIRES_KEY, state.expiresAt);
}

export function clearAuth(): void {
  if (typeof window === "undefined") return;
  localStorage.removeItem(TOKEN_KEY);
  localStorage.removeItem(EXPIRES_KEY);
}

export function getAccessToken(): string | null {
  if (typeof window === "undefined") return null;

  const token = localStorage.getItem(TOKEN_KEY);
  const expiresAt = localStorage.getItem(EXPIRES_KEY);

  if (!token || !expiresAt) return null;

  if (new Date(expiresAt) <= new Date()) {
    clearAuth();
    return null;
  }

  return token;
}

export function isAuthenticated(): boolean {
  return getAccessToken() !== null;
}

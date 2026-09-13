import type { AuthTokens, CurrentPrincipal } from '../api/generated';

const StorageKey = 'carelanka.session';

export type Session = {
  accessToken: string;
  refreshToken: string;
  principal: CurrentPrincipal;
};

let current: Session | null = read();

function read(): Session | null {
  const raw = sessionStorage.getItem(StorageKey);

  if (!raw) {
    return null;
  }

  try {
    return JSON.parse(raw) as Session;
  } catch {
    sessionStorage.removeItem(StorageKey);
    return null;
  }
}

const listeners = new Set<(session: Session | null) => void>();

export function getSession(): Session | null {
  return current;
}

export function getAccessToken(): string | null {
  return current?.accessToken ?? null;
}

export function setSession(tokens: AuthTokens): void {
  current = {
    accessToken: tokens.access_token,
    refreshToken: tokens.refresh_token,
    principal: tokens.principal,
  };

  sessionStorage.setItem(StorageKey, JSON.stringify(current));
  listeners.forEach((listener) => listener(current));
}

export function clearSession(): void {
  current = null;
  sessionStorage.removeItem(StorageKey);
  listeners.forEach((listener) => listener(null));
}

export function subscribe(listener: (session: Session | null) => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

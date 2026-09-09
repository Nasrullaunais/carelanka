import type { AuthTokens, CurrentPrincipal } from '../api/generated';

// Where the tokens live, and the one honest caveat about it.
//
// The API's own docs say to keep a refresh token in secure storage, never in localStorage.
// A browser has no secure storage — there is no equivalent of the Android keystore — so the
// real choices are memory only (gone on every page refresh) or sessionStorage (gone when the
// tab closes, and not shared with other tabs or with anything on another origin).
//
// sessionStorage, because a demo where every refresh logs you out is not testable. This is a
// known compromise for a coursework app, not a pattern to copy into something real.
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
    // A half-written or stale-shaped entry is not worth crashing the app over.
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

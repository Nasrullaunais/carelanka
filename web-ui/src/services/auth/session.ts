import type { AuthTokens, CurrentPrincipal } from '../api/generated';

const StorageKey = 'carelanka.session';

export type Session = {
  accessToken: string;
  refreshToken: string;
  principal: CurrentPrincipal;
  expiresAt: number;
};

const RefreshAheadMs = 60_000;
const RetryDelayMs = 30_000;
let refreshTimer: ReturnType<typeof setTimeout> | undefined;
let refreshPromise: Promise<boolean> | undefined;

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
    expiresAt: Date.now() + tokens.expires_in * 1_000,
  };

  sessionStorage.setItem(StorageKey, JSON.stringify(current));
  listeners.forEach((listener) => listener(current));
  scheduleRefresh();
}

export function clearSession(): void {
  current = null;
  clearTimeout(refreshTimer);
  sessionStorage.removeItem(StorageKey);
  listeners.forEach((listener) => listener(null));
}

function scheduleRefresh(delay?: number): void {
  clearTimeout(refreshTimer);
  if (!current) return;

  refreshTimer = setTimeout(
    () => { void refreshSession(); },
    delay ?? Math.max(0, (current.expiresAt ?? 0) - Date.now() - RefreshAheadMs),
  );
}

export function refreshSession(): Promise<boolean> {
  if (!current) return Promise.resolve(false);
  if (refreshPromise) return refreshPromise;

  const session = current;
  refreshPromise = (async () => {
    try {
      const response = await fetch('/api/auth/refresh', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refresh_token: session.refreshToken }),
      });

      // A sign-out or new sign-in may have happened while the request was in flight.
      if (current?.refreshToken !== session.refreshToken) return current !== null;

      if (response.ok) {
        setSession(await response.json() as AuthTokens);
        return true;
      }

      if ([400, 401, 403].includes(response.status)) {
        clearSession();
      } else {
        scheduleRefresh(RetryDelayMs);
      }
      return false;
    } catch {
      if (current?.refreshToken === session.refreshToken) scheduleRefresh(RetryDelayMs);
      return false;
    }
  })().finally(() => { refreshPromise = undefined; });

  return refreshPromise;
}

export function ensureFreshSession(): Promise<boolean> {
  if (!current) return Promise.resolve(false);
  if ((current.expiresAt ?? 0) - Date.now() > RefreshAheadMs) return Promise.resolve(true);
  return refreshSession();
}

export function subscribe(listener: (session: Session | null) => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

scheduleRefresh();

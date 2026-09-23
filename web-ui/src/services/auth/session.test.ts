import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { AuthTokens } from '../api/generated';

function tokens(access: string, refresh: string, expiresIn = 120): AuthTokens {
  return {
    access_token: access,
    refresh_token: refresh,
    expires_in: expiresIn,
    token_type: 'Bearer',
    principal: { id: 'staff-1', display_name: 'Staff' } as AuthTokens['principal'],
  };
}

describe('web session refresh', () => {
  beforeEach(() => {
    vi.resetModules();
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-09-24T00:00:00Z'));
    sessionStorage.clear();
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
  });

  it('rotates the token before expiry while the tab remains open', async () => {
    const fetchMock = vi.fn().mockResolvedValue(Response.json(tokens('new-access', 'new-refresh')));
    vi.stubGlobal('fetch', fetchMock);
    const session = await import('./session');

    session.setSession(tokens('old-access', 'old-refresh'));
    await vi.advanceTimersByTimeAsync(60_000);

    expect(fetchMock).toHaveBeenCalledOnce();
    expect(JSON.parse(String(fetchMock.mock.calls[0][1].body))).toEqual({ refresh_token: 'old-refresh' });
    expect(session.getAccessToken()).toBe('new-access');
    expect(session.getSession()?.refreshToken).toBe('new-refresh');
  });

  it('shares one refresh and never restores a session after sign-out', async () => {
    let respond!: (response: Response) => void;
    const fetchMock = vi.fn().mockImplementation(() => new Promise<Response>((resolve) => { respond = resolve; }));
    vi.stubGlobal('fetch', fetchMock);
    const session = await import('./session');
    session.setSession(tokens('old-access', 'old-refresh'));

    const first = session.refreshSession();
    const second = session.refreshSession();
    expect(fetchMock).toHaveBeenCalledOnce();

    session.clearSession();
    respond(Response.json(tokens('new-access', 'new-refresh')));
    await Promise.all([first, second]);
    expect(session.getSession()).toBeNull();
    expect(sessionStorage.getItem('carelanka.session')).toBeNull();
  });
});

import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import type { AuthTokens } from './generated';

beforeEach(() => {
  vi.resetModules();
  vi.useFakeTimers();
  sessionStorage.clear();
});

afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

it('refreshes and retries a protected request once after a 401', async () => {
  const credentials = {
    access_token: 'new-access',
    refresh_token: 'new-refresh',
    expires_in: 900,
    token_type: 'Bearer',
    principal: { id: 'staff-1', display_name: 'Staff' },
  } as AuthTokens;
  const fetchMock = vi.fn().mockImplementation((request: Request | string) => {
    if (typeof request === 'string') return Promise.resolve(Response.json(credentials));
    if (request.headers.get('Authorization') === 'Bearer old-access') {
      return Promise.resolve(new Response(null, { status: 401 }));
    }
    return Promise.resolve(Response.json({ ok: true }));
  });
  vi.stubGlobal('fetch', fetchMock);
  const session = await import('../auth/session');
  const { authenticatedFetch } = await import('./transport');
  session.setSession({ ...credentials, access_token: 'old-access', refresh_token: 'old-refresh' });

  const response = await authenticatedFetch(new Request('http://localhost/api/wards', {
    headers: { Authorization: 'Bearer old-access' },
  }));

  expect(response.status).toBe(200);
  expect(fetchMock).toHaveBeenCalledTimes(3);
  expect((fetchMock.mock.calls[2][0] as Request).headers.get('Authorization')).toBe('Bearer new-access');
});

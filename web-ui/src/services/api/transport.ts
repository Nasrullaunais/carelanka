import { toast } from 'sonner';
import { client } from './generated/client.gen';
import { ensureFreshSession, getAccessToken, getSession, refreshSession } from '../auth/session';
import { problemMessage } from './errors';
import type { ProblemDetails } from './generated';

client.interceptors.request.use(async (request) => {
  if (!isSignInPath(request) && !isLogoutPath(request)) await ensureFreshSession();
  const token = getAccessToken();

  if (token && !isSignInPath(request)) {
    request.headers.set('Authorization', `Bearer ${token}`);
  }

  return request;
});

// Keep one replayable copy so a 401 can be retried after a refresh. The refresh
// call uses the native fetch directly and never passes through this client.
export async function authenticatedFetch(input: RequestInfo | URL, init?: RequestInit): Promise<Response> {
  const request = input instanceof Request && !init ? input : new Request(input, init);
  const replay = request.clone();
  const response = await fetch(request);
  if (response.status !== 401 || isSignInPath(request) || isLogoutPath(request) || !getSession()) return response;

  const sentToken = request.headers.get('Authorization');
  const currentToken = getAccessToken();
  if (sentToken === `Bearer ${currentToken}` && !await refreshSession()) return response;

  const newToken = getAccessToken();
  if (!newToken) return response;
  replay.headers.set('Authorization', `Bearer ${newToken}`);
  return fetch(replay);
}

client.setConfig({ fetch: authenticatedFetch });

client.interceptors.error.use((error, response, request) => {
  if (request?.signal?.aborted || (error as { name?: string } | undefined)?.name === 'AbortError') {
    return error;
  }

  if (!response) {
    toast.error(
      import.meta.env.DEV
        ? 'Could not reach the server. Check that the API is running on port 5231 and that the Vite dev server is up - it is what proxies /api.'
        : 'Could not reach the server. Check your connection and try again.',
    );
    return error;
  }

  const isSignIn = isSignInPath(request);

  if (response.status === 401 && !isSignIn) {
    toast.error(getSession()
      ? 'Could not renew your session. Please try again.'
      : 'Your session has ended. Please sign in again.');
    return error;
  }

  if (response.status === 404 && isExpected404(request)) {
    return error;
  }

  if (response.status === 409) {
    return error;
  }

  toast.error(problemMessage(error as ProblemDetails) ?? `Request failed (${response.status}).`);

  return error;
});

const signInPaths = ['/auth/login', '/auth/patient/login', '/auth/patient/register'];

function isSignInPath(request: Request | undefined): boolean {
  return signInPaths.some((path) => pathOf(request).endsWith(path));
}

function isLogoutPath(request: Request | undefined): boolean {
  return pathOf(request).endsWith('/auth/logout');
}

// "No bill raised yet" is a normal state the panel renders itself, not a
// failure worth a toast.
const expected404s = [/\/admissions\/[^/]+\/bill$/, /\/appointments\/[^/]+\/bill$/];

function isExpected404(request: Request | undefined): boolean {
  if (request?.method !== 'GET') {
    return false;
  }

  const path = pathOf(request);

  return expected404s.some((pattern) => pattern.test(path));
}

function pathOf(request: Request | undefined): string {
  if (!request) {
    return '';
  }

  try {
    return new URL(request.url).pathname;
  } catch {
    return request.url;
  }
}

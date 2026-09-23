import { toast } from 'sonner';
import { client } from './generated/client.gen';
import { clearSession, getAccessToken } from '../auth/session';
import { problemMessage } from './errors';
import type { ProblemDetails } from './generated';

client.interceptors.request.use((request) => {
  const token = getAccessToken();

  if (token) {
    request.headers.set('Authorization', `Bearer ${token}`);
  }

  return request;
});

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

  const isSignIn = signInPaths.some((path) => pathOf(request).endsWith(path));

  if (response.status === 401 && !isSignIn) {
    clearSession();
    toast.error('Your session has ended. Please sign in again.');
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

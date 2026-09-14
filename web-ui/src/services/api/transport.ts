import { toast } from 'sonner';
import { client } from './generated/client.gen';
import { clearSession, getAccessToken } from '../auth/session';

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

  toast.error(messageOf(error) ?? `Request failed (${response.status}).`);

  return error;
});

const signInPaths = ['/auth/login', '/auth/patient/login', '/auth/patient/register'];

const expected404s = [/\/admissions\/[^/]+\/bill$/];

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

function messageOf(error: unknown): string | undefined {
  if (typeof error !== 'object' || error === null) {
    return typeof error === 'string' ? error : undefined;
  }

  const problem = error as { detail?: unknown; title?: unknown; errors?: unknown };
  const detail = typeof problem.detail === 'string' ? problem.detail : undefined;
  const fields = fieldErrors(problem.errors);

  if (detail && fields) {
    return `${detail} ${fields}`;
  }

  return detail ?? (typeof problem.title === 'string' ? problem.title : undefined);
}

function fieldErrors(errors: unknown): string | undefined {
  if (typeof errors !== 'object' || errors === null) {
    return undefined;
  }

  const messages = Object.values(errors as Record<string, unknown>)
    .flatMap((value) => (Array.isArray(value) ? value : []))
    .filter((value): value is string => typeof value === 'string');

  return messages.length > 0 ? messages.join(' ') : undefined;
}

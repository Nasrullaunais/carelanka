import { toast } from 'sonner';
import { client } from './generated/client.gen';
import { clearSession, getAccessToken } from '../auth/session';

// Every HTTP concern that is not the base URL lives here. A page never handles a status code.
// Imported once, for its side effects, from main.tsx — after the client exists.

client.interceptors.request.use((request) => {
  const token = getAccessToken();

  if (token) {
    request.headers.set('Authorization', `Bearer ${token}`);
  }

  return request;
});

client.interceptors.error.use((error, response, request) => {
  // An aborted request is not a failure and must never toast. TanStack Query cancels the
  // in-flight request whenever a query unmounts — which React StrictMode makes happen on
  // every single mount in development. This has no Response either, so it has to be ruled
  // out before the "cannot reach the server" branch below.
  if (request?.signal?.aborted || (error as { name?: string } | undefined)?.name === 'AbortError') {
    return error;
  }

  // A genuinely rejected fetch has no Response at all — the API is down, or the dev proxy
  // is not running. Handled explicitly, because a page that swallows this leaves the user
  // believing a write succeeded.
  if (!response) {
    // Two different things produce this, and in development the dev proxy is the likelier of
    // the two - the API can be perfectly healthy while nothing is forwarding /api to it. The
    // old wording named only the API and sent people to check the half that was working.
    toast.error(
      import.meta.env.DEV
        ? 'Could not reach the server. Check that the API is running on port 5231 and that the Vite dev server is up - it is what proxies /api.'
        : 'Could not reach the server. Check your connection and try again.',
    );
    return error;
  }

  // A 401 answering a sign-in attempt is not an expired session - it is the wrong email or
  // password, and the server already says so in ProblemDetails.detail. Telling someone at the
  // login screen that their session has ended sends them to do the thing they are already
  // doing, and hides the only sentence that would have helped. /auth/refresh is deliberately
  // not in this list: a 401 there really is a dead session.
  const isSignIn = signInPaths.some((path) => pathOf(request).endsWith(path));

  if (response.status === 401 && !isSignIn) {
    // Session gone. Not a toast the user can act on beyond signing in again.
    clearSession();
    toast.error('Your session has ended. Please sign in again.');
    return error;
  }

  // Everything else: show the server's own message, word for word. Re-wording it in the UI
  // is how two people end up describing the same failure differently.
  toast.error(messageOf(error) ?? `Request failed (${response.status}).`);

  return error;
});

const signInPaths = ['/auth/login', '/auth/patient/login', '/auth/patient/register'];

function pathOf(request: Request | undefined): string {
  if (!request) {
    return '';
  }

  try {
    return new URL(request.url).pathname;
  } catch {
    // A relative or malformed URL should never cost us the error toast entirely.
    return request.url;
  }
}

// ProblemDetails puts the human text in `detail`; a validation failure also carries a field
// map in `errors`, and naming the fields is more use than "one or more fields are not valid".
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

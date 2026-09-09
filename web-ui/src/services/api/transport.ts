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
    toast.error('Could not reach the server. Is the API running on port 5231?');
    return error;
  }

  if (response.status === 401) {
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

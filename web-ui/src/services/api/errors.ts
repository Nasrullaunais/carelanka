import type { ProblemDetails, ValidationProblemDetails } from './generated';

export type ApiProblem = ProblemDetails | ValidationProblemDetails;

export function isConflict(error: ApiProblem): boolean {
  return error.status === 409;
}

export function problemMessage(error: ApiProblem | null | undefined, fallback?: string): string | undefined {
  const detail = error?.detail ?? undefined;
  const fields = fieldErrors((error as ValidationProblemDetails | null | undefined)?.errors);
  if (detail && fields) return `${detail} ${fields}`;
  return detail ?? error?.title ?? fallback;
}

export function problemStringList(error: ApiProblem, field: string): string[] {
  const value = error[field];
  return Array.isArray(value) ? value.filter((item): item is string => typeof item === 'string') : [];
}

function fieldErrors(errors?: Record<string, string[]> | null): string | undefined {
  const messages = Object.values(errors ?? {}).flat();
  return messages.length > 0 ? messages.join(' ') : undefined;
}

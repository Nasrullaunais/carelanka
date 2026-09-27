import type { ReactNode } from 'react';
import type { UseQueryResult } from '@tanstack/react-query';
import { Alert, AlertContent, AlertDescription, AlertTitle, Button, Skeleton } from '@heroui/react';
import { problemMessage } from '../../services/api/errors';
import type { ApiProblem } from '../../services/api/errors';

export function QuerySkeleton({ rows = 3 }: { rows?: number }) {
  return (
    <div aria-busy="true" className="flex flex-col gap-2 py-2">
      {Array.from({ length: rows }, (_, index) => (
        <Skeleton key={index} className="h-8 w-full" />
      ))}
    </div>
  );
}

export function QueryError({ error, context, onRetry }: { error: ApiProblem; context: string; onRetry: () => void }) {
  return (
    <Alert status="danger">
      <AlertContent>
        <AlertTitle>{context}</AlertTitle>
        <AlertDescription>{problemMessage(error, 'Check your connection and try again.')}</AlertDescription>
      </AlertContent>
      <Button variant="outline" size="sm" onPress={onRetry}>
        Try again
      </Button>
    </Alert>
  );
}

export function QueryState<T, E extends ApiProblem = ApiProblem>({ query, isEmpty, emptyMessage = 'Nothing to show yet.', errorContext, skeletonRows, children }: {
  query: UseQueryResult<T, E>;
  isEmpty?: (data: T) => boolean;
  emptyMessage?: string;
  errorContext?: string;
  skeletonRows?: number;
  children: (data: T) => ReactNode;
}) {
  if (query.isPending) return <QuerySkeleton rows={skeletonRows} />;
  if (query.isError) {
    return <QueryError error={query.error} context={errorContext ?? 'Could not load this.'} onRetry={() => void query.refetch()} />;
  }
  const data = query.data;
  if (data === undefined) return null;
  if (isEmpty?.(data)) return <p className="muted">{emptyMessage}</p>;
  return <>{children(data)}</>;
}

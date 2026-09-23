import { createElement, type ReactNode } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';

export function queryOptionsMock<T>(key: readonly unknown[], data: T) {
  return {
    queryKey: key,
    queryFn: () => Promise.resolve(data),
  };
}

export function mutationMock(fn: (variables: unknown) => unknown) {
  return { mutationFn: fn };
}

export function pagedResult<T>(items: T[]) {
  return {
    items,
    page: 1,
    page_size: 50,
    total_items: items.length,
    total_pages: 1,
  };
}

export function renderWithProviders(node: ReactNode) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(createElement(QueryClientProvider, { client }, node));
}

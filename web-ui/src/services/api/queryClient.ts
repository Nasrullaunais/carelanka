import { QueryClient } from '@tanstack/react-query';

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // Queries do not retry. The interceptor already toasted the failure, so a retry means
      // the same toast three times and a page that stays blank for far longer. Lists render
      // "Try again" instead of an empty table.
      retry: false,
      refetchOnWindowFocus: false,
    },
  },
});

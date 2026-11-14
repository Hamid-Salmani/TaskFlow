import { QueryClient } from '@tanstack/react-query';
import { toApiError } from './errors';

export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 30_000,
        refetchOnWindowFocus: false,
        // Client errors (4xx) will not fix themselves; only retry network problems and 5xx.
        retry: (failureCount, error) => {
          const { status } = toApiError(error);
          return (status === null || status >= 500) && failureCount < 2;
        },
      },
    },
  });
}

export const queryClient = createQueryClient();

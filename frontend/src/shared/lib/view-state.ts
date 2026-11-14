import type { ApiError } from '@/shared/api/errors';
import { toApiError } from '@/shared/api/errors';

/** What a screen can be showing. Pages `switch` on `status`, so every case is handled and narrowed by the compiler. */
export type ViewState<T> =
  | { status: 'loading' }
  | { status: 'error'; error: ApiError; retry: () => void }
  | { status: 'empty' }
  | { status: 'ready'; data: T };

interface QueryLike<T> {
  isPending: boolean;
  isError: boolean;
  error: unknown;
  data: T | undefined;
  refetch: () => unknown;
}

export function toViewState<T>(query: QueryLike<T>, isEmpty?: (data: T) => boolean): ViewState<T> {
  if (query.isPending) {
    return { status: 'loading' };
  }

  if (query.isError || query.data === undefined) {
    return { status: 'error', error: toApiError(query.error), retry: () => void query.refetch() };
  }

  if (isEmpty?.(query.data)) {
    return { status: 'empty' };
  }

  return { status: 'ready', data: query.data };
}

import { AlertTriangle } from 'lucide-react';
import type { ApiError } from '@/shared/api/errors';
import { Button } from './Button';
import { EmptyState } from './EmptyState';

interface QueryErrorStateProps {
  error: ApiError;
  onRetry: () => void;
  /** Shown instead of the server message for 404s, which also cover "no access" (see NOTES.md). */
  notFoundMessage?: string;
}

export function QueryErrorState({ error, onRetry, notFoundMessage }: QueryErrorStateProps) {
  const isNotFound = error.status === 404;
  return (
    <div role="alert">
      <EmptyState
        icon={<AlertTriangle className="size-8" />}
        title={isNotFound ? 'Not found' : 'Something went wrong'}
        description={isNotFound ? (notFoundMessage ?? error.message) : error.message}
        action={
          isNotFound ? undefined : (
            <Button variant="outline" onClick={onRetry}>
              Try again
            </Button>
          )
        }
      />
    </div>
  );
}

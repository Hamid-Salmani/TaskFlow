import { formatRelativeTime } from '@/shared/lib/format';
import { toViewState } from '@/shared/lib/view-state';
import { QueryErrorState, Skeleton } from '@/shared/ui';
import { useCardActivity } from '../hooks/useComments';

export function ActivitySection({ cardId }: { cardId: string }) {
  const view = toViewState(useCardActivity(cardId));

  return (
    <section aria-labelledby="activity-heading" className="space-y-3">
      <h3 id="activity-heading" className="text-sm font-semibold">
        Activity
      </h3>
      {view.status === 'loading' ? <Skeleton className="h-12" /> : null}
      {view.status === 'error' ? <QueryErrorState error={view.error} onRetry={view.retry} /> : null}
      {view.status === 'ready' ? (
        <ul className="space-y-2 text-sm">
          {view.data.items.map((entry) => (
            <li key={entry.id}>
              <span className="font-medium">{entry.userDisplayName}</span> {entry.message}{' '}
              <span className="text-xs text-muted">
                {formatRelativeTime(entry.createdAt, new Date())}
              </span>
            </li>
          ))}
        </ul>
      ) : null}
    </section>
  );
}

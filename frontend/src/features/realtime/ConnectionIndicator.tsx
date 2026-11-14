import { WifiOff } from 'lucide-react';
import type { RealtimeStatus } from './useBoardRealtime';

/** Only speaks up when there is a problem; `role="status"` makes screen readers announce the change politely. */
export function ConnectionIndicator({ status }: { status: RealtimeStatus }) {
  const message =
    status === 'reconnecting'
      ? 'Reconnecting…'
      : status === 'disconnected'
        ? 'Offline — changes by others are not shown'
        : null;

  return (
    <div role="status" aria-live="polite">
      {message ? (
        <span className="inline-flex items-center gap-1.5 rounded-full bg-warning/15 px-3 py-1 text-xs font-medium text-warning">
          <WifiOff className="size-3.5" aria-hidden="true" />
          {message}
        </span>
      ) : null}
    </div>
  );
}

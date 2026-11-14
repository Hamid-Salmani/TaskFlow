import { AlertCircle, CheckCircle2, Info, X } from 'lucide-react';
import { useEffect } from 'react';
import { cn } from '@/shared/lib/cn';
import { useToastStore, type ToastItem, type ToastKind } from './toast-store';

const icons: Record<ToastKind, typeof Info> = {
  success: CheckCircle2,
  error: AlertCircle,
  info: Info,
};

const accents: Record<ToastKind, string> = {
  success: 'text-success',
  error: 'text-danger',
  info: 'text-primary',
};

function ToastView({ item }: { item: ToastItem }) {
  const dismiss = useToastStore((state) => state.dismiss);
  const Icon = icons[item.kind];

  useEffect(() => {
    const timer = window.setTimeout(() => dismiss(item.id), item.durationMs);
    return () => window.clearTimeout(timer);
  }, [dismiss, item.id, item.durationMs]);

  return (
    <div
      // Errors interrupt screen readers (role=alert); everything else is announced politely.
      role={item.kind === 'error' ? 'alert' : 'status'}
      className="pointer-events-auto flex w-full max-w-sm animate-slide-up items-start gap-3 rounded-md border border-border bg-surface p-3 shadow-popover"
    >
      <Icon className={cn('mt-0.5 size-5 shrink-0', accents[item.kind])} aria-hidden="true" />
      <div className="min-w-0 flex-1">
        <p className="text-sm font-medium">{item.title}</p>
        {item.description ? <p className="mt-0.5 text-sm text-muted">{item.description}</p> : null}
      </div>
      <button
        type="button"
        aria-label="Dismiss notification"
        onClick={() => dismiss(item.id)}
        className="rounded-sm p-0.5 text-muted hover:text-foreground"
      >
        <X className="size-4" aria-hidden="true" />
      </button>
    </div>
  );
}

/** Always mounted so assistive technology already knows the live region when the first toast appears. */
export function Toaster() {
  const toasts = useToastStore((state) => state.toasts);

  return (
    <div
      aria-live="polite"
      aria-atomic="false"
      aria-label="Notifications"
      role="region"
      className="pointer-events-none fixed inset-x-0 bottom-0 z-[60] flex flex-col items-center gap-2 p-4 sm:items-end"
    >
      {toasts.map((item) => (
        <ToastView key={item.id} item={item} />
      ))}
    </div>
  );
}

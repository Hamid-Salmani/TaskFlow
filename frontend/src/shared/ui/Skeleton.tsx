import type { HTMLAttributes } from 'react';
import { cn } from '@/shared/lib/cn';

/** Placeholder block. Decorative: screen readers get a single "Loading" status from the surrounding region. */
export function Skeleton({ className, ...props }: HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      aria-hidden="true"
      className={cn('animate-pulse rounded-md bg-surface-muted', className)}
      {...props}
    />
  );
}

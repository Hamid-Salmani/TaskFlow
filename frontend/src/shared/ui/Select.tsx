import { forwardRef, type SelectHTMLAttributes } from 'react';
import { cn } from '@/shared/lib/cn';

export type SelectProps = SelectHTMLAttributes<HTMLSelectElement>;

/** Native <select> with the shared control styling (keeps built-in keyboard and mobile behaviour). */
export const Select = forwardRef<HTMLSelectElement, SelectProps>(function Select(
  { className, ...props },
  ref,
) {
  return (
    <select
      ref={ref}
      className={cn(
        'h-10 w-full rounded-md border border-border bg-surface px-3 text-sm text-foreground disabled:opacity-60',
        className,
      )}
      {...props}
    />
  );
});

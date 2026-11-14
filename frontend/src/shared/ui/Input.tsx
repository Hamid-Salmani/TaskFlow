import {
  forwardRef,
  useId,
  type InputHTMLAttributes,
  type ReactNode,
  type TextareaHTMLAttributes,
} from 'react';
import { cn } from '@/shared/lib/cn';

const controlClasses =
  'w-full rounded-md border bg-surface px-3 text-sm text-foreground placeholder:text-muted disabled:cursor-not-allowed disabled:opacity-60';

export interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  invalid?: boolean;
}

export const Input = forwardRef<HTMLInputElement, InputProps>(function Input(
  { className, invalid = false, ...props },
  ref,
) {
  return (
    <input
      ref={ref}
      aria-invalid={invalid || undefined}
      className={cn(controlClasses, 'h-10', invalid ? 'border-danger' : 'border-border', className)}
      {...props}
    />
  );
});

export interface TextareaProps extends TextareaHTMLAttributes<HTMLTextAreaElement> {
  invalid?: boolean;
}

export const Textarea = forwardRef<HTMLTextAreaElement, TextareaProps>(function Textarea(
  { className, invalid = false, ...props },
  ref,
) {
  return (
    <textarea
      ref={ref}
      aria-invalid={invalid || undefined}
      className={cn(
        controlClasses,
        'min-h-24 py-2',
        invalid ? 'border-danger' : 'border-border',
        className,
      )}
      {...props}
    />
  );
});

interface FieldFrameProps {
  label: string;
  error?: string | undefined;
  hint?: string;
  className?: string;
  children: (ids: { id: string; describedBy: string | undefined; invalid: boolean }) => ReactNode;
}

/** Label + control + error wiring (id, aria-describedby, aria-invalid) shared by every form field. */
function FieldFrame({ label, error, hint, className, children }: FieldFrameProps) {
  const id = useId();
  const errorId = `${id}-error`;
  const hintId = `${id}-hint`;
  const describedBy = [error ? errorId : null, hint ? hintId : null].filter(Boolean).join(' ');

  return (
    <div className={cn('space-y-1.5', className)}>
      <label htmlFor={id} className="block text-sm font-medium">
        {label}
      </label>
      {children({ id, describedBy: describedBy || undefined, invalid: Boolean(error) })}
      {hint && !error ? (
        <p id={hintId} className="text-xs text-muted">
          {hint}
        </p>
      ) : null}
      {error ? (
        <p id={errorId} role="alert" className="text-xs font-medium text-danger">
          {error}
        </p>
      ) : null}
    </div>
  );
}

export interface TextFieldProps extends Omit<InputProps, 'invalid'> {
  label: string;
  error?: string | undefined;
  hint?: string;
  containerClassName?: string;
}

export const TextField = forwardRef<HTMLInputElement, TextFieldProps>(function TextField(
  { label, error, hint, containerClassName, ...props },
  ref,
) {
  return (
    <FieldFrame label={label} error={error} hint={hint} className={containerClassName ?? ''}>
      {({ id, describedBy, invalid }) => (
        <Input ref={ref} id={id} aria-describedby={describedBy} invalid={invalid} {...props} />
      )}
    </FieldFrame>
  );
});

export interface TextareaFieldProps extends Omit<TextareaProps, 'invalid'> {
  label: string;
  error?: string | undefined;
  hint?: string;
  containerClassName?: string;
}

export const TextareaField = forwardRef<HTMLTextAreaElement, TextareaFieldProps>(
  function TextareaField({ label, error, hint, containerClassName, ...props }, ref) {
    return (
      <FieldFrame label={label} error={error} hint={hint} className={containerClassName ?? ''}>
        {({ id, describedBy, invalid }) => (
          <Textarea ref={ref} id={id} aria-describedby={describedBy} invalid={invalid} {...props} />
        )}
      </FieldFrame>
    );
  },
);

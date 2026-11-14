import { zodResolver } from '@hookform/resolvers/zod';
import { useEffect, useRef, type KeyboardEvent } from 'react';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { toApiError } from '@/shared/api/errors';
import { Button } from '@/shared/ui';

interface InlineTitleFormProps {
  /** Accessible name of the text field. */
  label: string;
  submitLabel: string;
  maxLength: number;
  initialValue?: string;
  /** Rejects to keep the form open and show the message. */
  onSubmit: (title: string) => Promise<unknown>;
  /** Without it the form is always visible: no Cancel button, Escape is left to the surrounding dialog. */
  onCancel?: () => void;
  /** Focus the field when the form appears (off for composers that are visible all the time). */
  focusOnMount?: boolean;
  /** Keep the form open and clear the field after a successful submit (adding several cards in a row). */
  keepOpen?: boolean;
  multiline?: boolean;
}

/** One-field form used to add a card, add a column and rename a column. Enter submits, Escape cancels. */
export function InlineTitleForm({
  label,
  submitLabel,
  maxLength,
  initialValue = '',
  onSubmit,
  onCancel,
  focusOnMount = true,
  keepOpen = false,
  multiline = false,
}: InlineTitleFormProps) {
  const schema = z.object({
    title: z
      .string()
      .trim()
      .min(1, 'Enter a title')
      .max(maxLength, `At most ${maxLength} characters`),
  });
  const {
    register,
    handleSubmit,
    reset,
    setFocus,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<z.infer<typeof schema>>({
    resolver: zodResolver(schema),
    defaultValues: { title: initialValue },
  });
  const { ref: registerRef, ...field } = register('title');
  const fieldRef = useRef<HTMLInputElement | HTMLTextAreaElement | null>(null);

  useEffect(() => {
    if (focusOnMount) {
      setFocus('title', { shouldSelect: true });
    }
  }, [focusOnMount, setFocus]);

  const submit = handleSubmit(async ({ title }) => {
    try {
      await onSubmit(title);
      if (keepOpen) {
        reset({ title: '' });
        // After the submit button re-enables; focusing synchronously can be undone by that re-render.
        requestAnimationFrame(() => setFocus('title'));
      } else {
        onCancel?.();
      }
    } catch (error) {
      setError('title', { message: toApiError(error).message });
    }
  });

  function handleKeyDown(event: KeyboardEvent) {
    if (event.key === 'Escape' && onCancel) {
      event.stopPropagation();
      onCancel();
    } else if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      void submit();
    }
  }

  const sharedProps = {
    ...field,
    'aria-label': label,
    'aria-invalid': errors.title ? true : undefined,
    'aria-describedby': errors.title ? `${label}-error` : undefined,
    onKeyDown: handleKeyDown,
    className:
      'w-full rounded-md border border-border bg-surface px-2 py-1.5 text-sm shadow-sm placeholder:text-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring aria-[invalid=true]:border-danger',
  };

  return (
    <form onSubmit={submit} noValidate className="space-y-2">
      {multiline ? (
        <textarea
          {...sharedProps}
          rows={2}
          placeholder={label}
          ref={(element) => {
            registerRef(element);
            fieldRef.current = element;
          }}
        />
      ) : (
        <input
          {...sharedProps}
          type="text"
          placeholder={label}
          autoComplete="off"
          ref={(element) => {
            registerRef(element);
            fieldRef.current = element;
          }}
        />
      )}
      {errors.title ? (
        <p id={`${label}-error`} role="alert" className="text-xs font-medium text-danger">
          {errors.title.message}
        </p>
      ) : null}
      <div className="flex items-center gap-2">
        <Button type="submit" size="sm" loading={isSubmitting}>
          {submitLabel}
        </Button>
        {onCancel ? (
          <Button size="sm" variant="ghost" onClick={onCancel}>
            Cancel
          </Button>
        ) : null}
      </div>
    </form>
  );
}

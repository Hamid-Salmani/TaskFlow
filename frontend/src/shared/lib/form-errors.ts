import type { FieldValues, Path, UseFormSetError } from 'react-hook-form';
import type { ApiError } from '@/shared/api/errors';

/**
 * Copies server-side validation errors (`errors: { fieldName: [...] }`) onto the matching form fields.
 * Returns true when at least one field was set, so the caller can fall back to a form-level message.
 */
export function applyServerFieldErrors<T extends FieldValues>(
  error: ApiError,
  fields: readonly Path<T>[],
  setError: UseFormSetError<T>,
): boolean {
  let applied = false;
  for (const field of fields) {
    const message = error.fieldErrors[field]?.[0];
    if (message) {
      setError(field, { type: 'server', message });
      applied = true;
    }
  }
  return applied;
}

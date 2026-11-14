import { zodResolver } from '@hookform/resolvers/zod';
import { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { toApiError } from '@/shared/api/errors';
import { applyServerFieldErrors } from '@/shared/lib/form-errors';
import { Button, Modal, TextareaField, TextField } from '@/shared/ui';
import { nameDescriptionSchema, type NameDescriptionValues } from '../schemas';

interface NameDescriptionModalProps {
  open: boolean;
  onClose: () => void;
  title: string;
  submitLabel: string;
  nameLabel?: string;
  initialValues?: Partial<NameDescriptionValues>;
  /** Rejects with an error to keep the dialog open and show it; resolves to close. */
  onSubmit: (values: NameDescriptionValues) => Promise<unknown>;
}

/** Shared by "create workspace", "create board" and the two rename dialogs. */
export function NameDescriptionModal({
  open,
  onClose,
  title,
  submitLabel,
  nameLabel = 'Name',
  initialValues,
  onSubmit,
}: NameDescriptionModalProps) {
  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<NameDescriptionValues>({
    resolver: zodResolver(nameDescriptionSchema),
    defaultValues: { name: '', description: '' },
  });

  useEffect(() => {
    if (open) {
      reset({ name: initialValues?.name ?? '', description: initialValues?.description ?? '' });
    }
  }, [open, initialValues?.name, initialValues?.description, reset]);

  const submit = handleSubmit(async (values) => {
    try {
      await onSubmit(values);
      onClose();
    } catch (error) {
      const apiError = toApiError(error);
      if (!applyServerFieldErrors(apiError, ['name', 'description'], setError)) {
        setError('root', { message: apiError.message });
      }
    }
  });

  return (
    <Modal open={open} onClose={onClose} title={title}>
      <form onSubmit={submit} noValidate className="space-y-4">
        {errors.root ? (
          <p
            role="alert"
            className="rounded-md bg-danger/10 px-3 py-2 text-sm font-medium text-danger"
          >
            {errors.root.message}
          </p>
        ) : null}
        <TextField
          label={nameLabel}
          data-autofocus
          autoComplete="off"
          error={errors.name?.message}
          {...register('name')}
        />
        <TextareaField
          label="Description (optional)"
          rows={3}
          error={errors.description?.message}
          {...register('description')}
        />
        <div className="flex justify-end gap-2">
          <Button variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" loading={isSubmitting}>
            {submitLabel}
          </Button>
        </div>
      </form>
    </Modal>
  );
}

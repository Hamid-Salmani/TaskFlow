import { zodResolver } from '@hookform/resolvers/zod';
import { Trash2 } from 'lucide-react';
import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { hasRole } from '@/features/workspaces/permissions';
import type { CardDto, LabelDto, WorkspaceMemberDto, WorkspaceRole } from '@/shared/api/types';
import { toApiError } from '@/shared/api/errors';
import { cn } from '@/shared/lib/cn';
import { fromDateInputValue, toDateInputValue } from '@/shared/lib/format';
import { selectUser, useSessionStore } from '@/shared/store/session-store';
import {
  Avatar,
  Button,
  ColorBadge,
  ConfirmDialog,
  Modal,
  TextareaField,
  TextField,
  toast,
} from '@/shared/ui';
import {
  useDeleteCard,
  useSetCardAssignees,
  useSetCardLabels,
  useUpdateCard,
} from '../hooks/useCardMutations';
import { ActivitySection } from './ActivitySection';
import { CommentsSection } from './CommentsSection';

const detailsSchema = z.object({
  title: z.string().trim().min(1, 'Title is required').max(200, 'At most 200 characters'),
  description: z.string().max(10_000, 'At most 10,000 characters'),
  dueDate: z.string(),
});
type DetailsValues = z.infer<typeof detailsSchema>;

interface CardModalProps {
  boardId: string;
  /** `undefined` once the cards are loaded and the card is gone (deleted elsewhere). */
  card: CardDto | undefined;
  labels: LabelDto[];
  members: WorkspaceMemberDto[];
  myRole: WorkspaceRole;
  /** Archived boards are read-only. */
  readOnly: boolean;
  onClose: () => void;
}

export function CardModal({
  boardId,
  card,
  labels,
  members,
  myRole,
  readOnly,
  onClose,
}: CardModalProps) {
  useEffect(() => {
    if (card === undefined) {
      onClose();
    }
  }, [card, onClose]);

  if (card === undefined) {
    return null;
  }

  return (
    <Modal open onClose={onClose} title={card.title} size="xl" hideTitle>
      {/* Remounted per version so the form always shows what the server has. */}
      <CardDetails
        key={`${card.id}:${card.version}`}
        boardId={boardId}
        card={card}
        labels={labels}
        members={members}
        myRole={myRole}
        readOnly={readOnly}
        onClose={onClose}
      />
    </Modal>
  );
}

function CardDetails({
  boardId,
  card,
  labels,
  members,
  myRole,
  readOnly,
  onClose,
}: Omit<CardModalProps, 'card'> & { card: CardDto }) {
  const user = useSessionStore(selectUser);
  const updateCard = useUpdateCard(boardId);
  const setLabels = useSetCardLabels(boardId);
  const setAssignees = useSetCardAssignees(boardId);
  const deleteCard = useDeleteCard(boardId);
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isDirty },
  } = useForm<DetailsValues>({
    resolver: zodResolver(detailsSchema),
    defaultValues: {
      title: card.title,
      description: card.description ?? '',
      dueDate: toDateInputValue(card.dueDate),
    },
  });

  const save = handleSubmit(async (values) => {
    setFormError(null);
    try {
      await updateCard.mutateAsync({
        cardId: card.id,
        expectedVersion: card.version,
        title: values.title.trim(),
        description: values.description.trim() === '' ? null : values.description.trim(),
        dueDate: fromDateInputValue(values.dueDate),
      });
      toast.success('Card saved');
    } catch (error) {
      const apiError = toApiError(error);
      if (!apiError.isConflict) {
        setFormError(apiError.message);
      }
    }
  });

  function toggle(ids: readonly string[], id: string): string[] {
    return ids.includes(id) ? ids.filter((existing) => existing !== id) : [...ids, id];
  }

  const busy = setLabels.isPending || setAssignees.isPending;

  return (
    <div className="grid gap-6 md:grid-cols-[minmax(0,1fr)_16rem]">
      <div className="space-y-6">
        <form onSubmit={save} noValidate className="space-y-3" aria-label="Card details">
          <TextField
            label="Title"
            disabled={readOnly}
            error={errors.title?.message}
            {...register('title')}
          />
          <TextareaField
            label="Description"
            rows={5}
            disabled={readOnly}
            error={errors.description?.message}
            {...register('description')}
          />
          <TextField label="Due date" type="date" disabled={readOnly} {...register('dueDate')} />
          {formError ? (
            <p role="alert" className="text-sm font-medium text-danger">
              {formError}
            </p>
          ) : null}
          {readOnly ? (
            <p className="text-sm text-muted">This board is archived, so the card is read-only.</p>
          ) : (
            <div className="flex items-center gap-2">
              <Button type="submit" loading={updateCard.isPending} disabled={!isDirty}>
                Save changes
              </Button>
              <Button variant="ghost" onClick={() => setConfirmingDelete(true)}>
                <Trash2 className="size-4" aria-hidden="true" />
                Delete card
              </Button>
            </div>
          )}
        </form>

        <CommentsSection
          boardId={boardId}
          cardId={card.id}
          currentUserId={user?.id ?? null}
          canModerate={hasRole(myRole, 'Admin')}
          readOnly={readOnly}
        />
        <ActivitySection cardId={card.id} />
      </div>

      <aside className="space-y-6">
        <section aria-labelledby="labels-heading">
          <h3 id="labels-heading" className="mb-2 text-sm font-semibold">
            Labels
          </h3>
          {labels.length === 0 ? (
            <p className="text-sm text-muted">No labels on this board.</p>
          ) : null}
          <ul className="flex flex-wrap gap-1.5">
            {labels.map((label) => {
              const selected = card.labelIds.includes(label.id);
              return (
                <li key={label.id}>
                  <button
                    type="button"
                    aria-pressed={selected}
                    disabled={readOnly || busy}
                    onClick={() =>
                      setLabels.mutate({
                        cardId: card.id,
                        expectedVersion: card.version,
                        labelIds: toggle(card.labelIds, label.id),
                      })
                    }
                    className={cn(
                      'rounded-full focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:opacity-60',
                      selected ? 'ring-2 ring-foreground' : 'opacity-60 hover:opacity-100',
                    )}
                  >
                    <ColorBadge color={label.color}>{label.name}</ColorBadge>
                  </button>
                </li>
              );
            })}
          </ul>
        </section>

        <section aria-labelledby="assignees-heading">
          <h3 id="assignees-heading" className="mb-2 text-sm font-semibold">
            Assignees
          </h3>
          <ul className="space-y-1">
            {members.map((member) => {
              const selected = card.assigneeIds.includes(member.userId);
              return (
                <li key={member.userId}>
                  <button
                    type="button"
                    aria-pressed={selected}
                    disabled={readOnly || busy}
                    onClick={() =>
                      setAssignees.mutate({
                        cardId: card.id,
                        expectedVersion: card.version,
                        userIds: toggle(card.assigneeIds, member.userId),
                      })
                    }
                    className={cn(
                      'flex w-full items-center gap-2 rounded-md px-2 py-1 text-left text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:opacity-60',
                      selected ? 'bg-primary/10 font-medium' : 'hover:bg-surface-muted',
                    )}
                  >
                    <Avatar name={member.displayName} size="sm" />
                    <span className="min-w-0 flex-1 truncate">{member.displayName}</span>
                    {selected ? <span className="text-xs text-primary">Assigned</span> : null}
                  </button>
                </li>
              );
            })}
          </ul>
        </section>
      </aside>

      <ConfirmDialog
        open={confirmingDelete}
        title="Delete card"
        description={`"${card.title}" and its comments will be deleted.`}
        confirmLabel="Delete card"
        destructive
        loading={deleteCard.isPending}
        onCancel={() => setConfirmingDelete(false)}
        onConfirm={() =>
          deleteCard.mutate(card.id, {
            onSuccess: () => {
              setConfirmingDelete(false);
              toast.success('Card deleted');
              onClose();
            },
            onError: (error) => toast.error('Could not delete the card', error.message),
          })
        }
      />
    </div>
  );
}

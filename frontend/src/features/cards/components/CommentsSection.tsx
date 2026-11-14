import { Pencil, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { splitMentions } from '@/features/cards/lib/mentions';
import type { CommentDto } from '@/shared/api/types';
import { formatRelativeTime } from '@/shared/lib/format';
import { toViewState } from '@/shared/lib/view-state';
import { Avatar, Button, ConfirmDialog, QueryErrorState, Skeleton, toast } from '@/shared/ui';
import {
  useAddComment,
  useComments,
  useDeleteComment,
  useUpdateComment,
} from '../hooks/useComments';
import { InlineTitleForm } from '@/features/boards/components/InlineTitleForm';

interface CommentsSectionProps {
  boardId: string;
  cardId: string;
  currentUserId: string | null;
  /** Admins and owners may delete other people's comments. */
  canModerate: boolean;
  readOnly: boolean;
}

function CommentBody({ body }: { body: string }) {
  return (
    <p className="whitespace-pre-wrap break-words text-sm">
      {splitMentions(body).map((segment, index) =>
        segment.kind === 'mention' ? (
          <span key={index} className="font-medium text-primary">
            {segment.text}
          </span>
        ) : (
          <span key={index}>{segment.text}</span>
        ),
      )}
    </p>
  );
}

export function CommentsSection({
  boardId,
  cardId,
  currentUserId,
  canModerate,
  readOnly,
}: CommentsSectionProps) {
  const comments = useComments(cardId);
  const add = useAddComment(boardId, cardId);
  const update = useUpdateComment(cardId);
  const remove = useDeleteComment(boardId, cardId);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [deleting, setDeleting] = useState<CommentDto | null>(null);
  const view = toViewState(comments);

  return (
    <section aria-labelledby="comments-heading" className="space-y-3">
      <h3 id="comments-heading" className="text-sm font-semibold">
        Comments
      </h3>

      {readOnly ? null : (
        <InlineTitleForm
          label="Write a comment"
          submitLabel="Comment"
          maxLength={5000}
          multiline
          keepOpen
          focusOnMount={false}
          onSubmit={(body) => add.mutateAsync(body)}
        />
      )}

      {view.status === 'loading' ? <Skeleton className="h-16" /> : null}
      {view.status === 'error' ? <QueryErrorState error={view.error} onRetry={view.retry} /> : null}
      {view.status === 'ready' && view.data.items.length === 0 ? (
        <p className="text-sm text-muted">No comments yet.</p>
      ) : null}
      {view.status === 'ready' ? (
        <ul className="space-y-3">
          {view.data.items.map((comment) => {
            const isAuthor = comment.authorId === currentUserId;
            return (
              <li key={comment.id} className="flex gap-2">
                <Avatar name={comment.authorDisplayName} size="sm" />
                <div className="min-w-0 flex-1">
                  <p className="text-xs text-muted">
                    <span className="font-medium text-foreground">{comment.authorDisplayName}</span>{' '}
                    {formatRelativeTime(comment.createdAt, new Date())}
                    {comment.editedAt ? ' (edited)' : ''}
                  </p>
                  {editingId === comment.id ? (
                    <InlineTitleForm
                      label="Edit comment"
                      submitLabel="Save"
                      maxLength={5000}
                      multiline
                      initialValue={comment.body}
                      onCancel={() => setEditingId(null)}
                      onSubmit={(body) => update.mutateAsync({ commentId: comment.id, body })}
                    />
                  ) : (
                    <CommentBody body={comment.body} />
                  )}
                  {readOnly || editingId === comment.id ? null : (
                    <div className="mt-1 flex gap-1">
                      {isAuthor ? (
                        <Button
                          size="sm"
                          variant="ghost"
                          onClick={() => setEditingId(comment.id)}
                          aria-label={`Edit comment by ${comment.authorDisplayName}`}
                        >
                          <Pencil className="size-3.5" aria-hidden="true" />
                          Edit
                        </Button>
                      ) : null}
                      {isAuthor || canModerate ? (
                        <Button
                          size="sm"
                          variant="ghost"
                          onClick={() => setDeleting(comment)}
                          aria-label={`Delete comment by ${comment.authorDisplayName}`}
                        >
                          <Trash2 className="size-3.5" aria-hidden="true" />
                          Delete
                        </Button>
                      ) : null}
                    </div>
                  )}
                </div>
              </li>
            );
          })}
        </ul>
      ) : null}

      <ConfirmDialog
        open={deleting !== null}
        title="Delete comment"
        description="This comment will be removed for everyone."
        confirmLabel="Delete comment"
        destructive
        loading={remove.isPending}
        onCancel={() => setDeleting(null)}
        onConfirm={() => {
          if (deleting === null) {
            return;
          }
          remove.mutate(deleting.id, {
            onSuccess: () => setDeleting(null),
            onError: (error) => toast.error('Could not delete the comment', error.message),
          });
        }}
      />
    </section>
  );
}

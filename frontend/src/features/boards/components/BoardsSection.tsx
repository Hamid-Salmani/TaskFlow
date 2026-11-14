import { LayoutGrid, Plus } from 'lucide-react';
import { useState } from 'react';
import type { BoardSummaryDto, WorkspaceRole } from '@/shared/api/types';
import { toViewState } from '@/shared/lib/view-state';
import { Button, ConfirmDialog, EmptyState, QueryErrorState, Skeleton, toast } from '@/shared/ui';
import { NameDescriptionModal } from '@/features/workspaces/components/NameDescriptionModal';
import { can } from '@/features/workspaces/permissions';
import { useArchiveBoard } from '../hooks/useArchiveBoard';
import { useBoards } from '../hooks/useBoards';
import { useCreateBoard } from '../hooks/useCreateBoard';
import { useDeleteBoard } from '../hooks/useDeleteBoard';
import { useUpdateBoard } from '../hooks/useUpdateBoard';
import { BoardCard } from './BoardCard';

interface BoardsSectionProps {
  workspaceId: string;
  role: WorkspaceRole;
}

export function BoardsSection({ workspaceId, role }: BoardsSectionProps) {
  const [showArchived, setShowArchived] = useState(false);
  const [creating, setCreating] = useState(false);
  const [renaming, setRenaming] = useState<BoardSummaryDto | null>(null);
  const [deleting, setDeleting] = useState<BoardSummaryDto | null>(null);

  const boards = useBoards(workspaceId, showArchived);
  const createBoard = useCreateBoard(workspaceId);
  const archiveBoard = useArchiveBoard(workspaceId);
  const deleteBoard = useDeleteBoard(workspaceId);
  const view = toViewState(boards, (data) => data.length === 0);
  const canCreate = can(role, 'createBoard');
  const canManage = can(role, 'manageBoards');

  const newBoardButton = canCreate ? (
    <Button size="sm" onClick={() => setCreating(true)}>
      <Plus className="size-4" aria-hidden="true" />
      New board
    </Button>
  ) : null;

  function toggleArchive(board: BoardSummaryDto) {
    archiveBoard.mutate(
      { boardId: board.id, archived: !board.isArchived },
      {
        onSuccess: () => toast.success(board.isArchived ? 'Board restored' : 'Board archived'),
        onError: (error) => toast.error('Could not update the board', error.message),
      },
    );
  }

  return (
    <section aria-labelledby="boards-heading">
      <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
        <h2 id="boards-heading" className="text-lg font-semibold">
          Boards
        </h2>
        <div className="flex items-center gap-4">
          <label className="flex items-center gap-2 text-sm text-muted">
            <input
              type="checkbox"
              className="accent-primary"
              checked={showArchived}
              onChange={(event) => setShowArchived(event.target.checked)}
            />
            Show archived
          </label>
          {newBoardButton}
        </div>
      </div>

      {view.status === 'loading' ? (
        <ul className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3" aria-label="Loading boards">
          {[0, 1, 2].map((key) => (
            <li key={key}>
              <Skeleton className="h-20" />
            </li>
          ))}
        </ul>
      ) : null}
      {view.status === 'error' ? <QueryErrorState error={view.error} onRetry={view.retry} /> : null}
      {view.status === 'empty' ? (
        <EmptyState
          icon={<LayoutGrid className="size-8" />}
          title="No boards yet"
          description={
            canCreate
              ? 'Create a board to start organizing work.'
              : 'Boards will show up here once someone creates them.'
          }
          action={newBoardButton}
        />
      ) : null}
      {view.status === 'ready' ? (
        <ul className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {view.data.map((board) => (
            <BoardCard
              key={board.id}
              board={board}
              canManage={canManage}
              onRename={setRenaming}
              onToggleArchive={toggleArchive}
              onDelete={setDeleting}
            />
          ))}
        </ul>
      ) : null}

      <NameDescriptionModal
        open={creating}
        onClose={() => setCreating(false)}
        title="New board"
        submitLabel="Create board"
        onSubmit={(values) =>
          createBoard.mutateAsync({ name: values.name, description: values.description || null })
        }
      />
      <RenameBoardModal
        workspaceId={workspaceId}
        board={renaming}
        onClose={() => setRenaming(null)}
      />
      <ConfirmDialog
        open={deleting !== null}
        title="Delete board"
        description={`"${deleting?.name ?? ''}" and all of its cards will be deleted. This cannot be undone from the app.`}
        confirmLabel="Delete board"
        destructive
        loading={deleteBoard.isPending}
        onCancel={() => setDeleting(null)}
        onConfirm={() => {
          if (deleting === null) {
            return;
          }
          deleteBoard.mutate(deleting.id, {
            onSuccess: () => {
              toast.success('Board deleted');
              setDeleting(null);
            },
            onError: (error) => toast.error('Could not delete the board', error.message),
          });
        }}
      />
    </section>
  );
}

function RenameBoardModal({
  workspaceId,
  board,
  onClose,
}: {
  workspaceId: string;
  board: BoardSummaryDto | null;
  onClose: () => void;
}) {
  const update = useUpdateBoard(workspaceId, board?.id ?? '');
  return (
    <NameDescriptionModal
      open={board !== null}
      onClose={onClose}
      title="Edit board"
      submitLabel="Save"
      initialValues={{ name: board?.name ?? '', description: board?.description ?? '' }}
      onSubmit={(values) =>
        update.mutateAsync({ name: values.name, description: values.description || null })
      }
    />
  );
}

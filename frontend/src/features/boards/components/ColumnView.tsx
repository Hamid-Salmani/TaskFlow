import { useDroppable } from '@dnd-kit/core';
import { SortableContext, useSortable, verticalListSortingStrategy } from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import { GripVertical, MoreHorizontal, Pencil, Plus, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { useCreateCard } from '@/features/cards/hooks/useCreateCard';
import type { CardDto, ColumnDto, LabelDto, WorkspaceMemberDto } from '@/shared/api/types';
import { cn } from '@/shared/lib/cn';
import { Button, ConfirmDialog, Dropdown, toast } from '@/shared/ui';
import { useDeleteColumn } from '../hooks/useDeleteColumn';
import { useRenameColumn } from '../hooks/useRenameColumn';
import { CardItem } from './CardItem';
import { InlineTitleForm } from './InlineTitleForm';

interface ColumnViewProps {
  boardId: string;
  column: ColumnDto;
  cards: CardDto[];
  labelsById: ReadonlyMap<string, LabelDto>;
  membersById: ReadonlyMap<string, WorkspaceMemberDto>;
  readOnly: boolean;
  canDelete: boolean;
  onOpenCard: (cardId: string) => void;
}

export function ColumnView({
  boardId,
  column,
  cards,
  labelsById,
  membersById,
  readOnly,
  canDelete,
  onOpenCard,
}: ColumnViewProps) {
  const [renaming, setRenaming] = useState(false);
  const [adding, setAdding] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const renameColumn = useRenameColumn(boardId);
  const deleteColumn = useDeleteColumn(boardId);
  const createCard = useCreateCard(boardId);

  const sortable = useSortable({ id: column.id, data: { type: 'column' }, disabled: readOnly });
  const body = useDroppable({
    id: `body:${column.id}`,
    data: { type: 'column-body', columnId: column.id },
  });

  const menuItems = [
    {
      id: 'rename',
      label: 'Rename',
      icon: <Pencil className="size-4" />,
      onSelect: () => setRenaming(true),
    },
    ...(canDelete
      ? [
          {
            id: 'delete',
            label: 'Delete column',
            icon: <Trash2 className="size-4" />,
            destructive: true,
            onSelect: () => setDeleting(true),
          },
        ]
      : []),
  ];

  return (
    <section
      ref={sortable.setNodeRef}
      aria-label={`${column.title}, ${cards.length} ${cards.length === 1 ? 'card' : 'cards'}`}
      style={{
        transform: CSS.Transform.toString(sortable.transform),
        transition: sortable.transition,
      }}
      className={cn(
        'flex max-h-full w-[85vw] shrink-0 snap-start flex-col rounded-lg bg-surface-muted/60 sm:w-72',
        sortable.isDragging && 'opacity-40',
      )}
    >
      <header className="flex items-center gap-1 px-2 pb-1 pt-2">
        {readOnly ? null : (
          <button
            type="button"
            ref={sortable.setActivatorNodeRef}
            {...sortable.attributes}
            {...sortable.listeners}
            aria-label={`Move column ${column.title}`}
            className="cursor-grab rounded p-1 text-muted hover:bg-surface-muted active:cursor-grabbing"
          >
            <GripVertical className="size-4" aria-hidden="true" />
          </button>
        )}
        {renaming ? (
          <div className="min-w-0 flex-1">
            <InlineTitleForm
              label="Column title"
              submitLabel="Save"
              maxLength={100}
              initialValue={column.title}
              onCancel={() => setRenaming(false)}
              onSubmit={(title) =>
                title === column.title
                  ? Promise.resolve()
                  : renameColumn.mutateAsync({ columnId: column.id, title })
              }
            />
          </div>
        ) : (
          <>
            <h2 className="min-w-0 flex-1 truncate px-1 text-sm font-semibold">{column.title}</h2>
            <span
              className="rounded-full bg-surface px-2 py-0.5 text-xs text-muted"
              aria-hidden="true"
            >
              {cards.length}
            </span>
            {readOnly ? null : (
              <Dropdown
                triggerLabel={`Actions for column ${column.title}`}
                trigger={<MoreHorizontal className="size-4" />}
                triggerClassName="size-8 justify-center"
                align="end"
                items={menuItems}
              />
            )}
          </>
        )}
      </header>

      <div ref={body.setNodeRef} className="min-h-12 flex-1 overflow-y-auto px-2 pb-2">
        <SortableContext
          items={cards.map((card) => card.id)}
          strategy={verticalListSortingStrategy}
        >
          <ul className="flex min-h-10 flex-col gap-2">
            {cards.map((card) => (
              <CardItem
                key={card.id}
                card={card}
                labels={card.labelIds.flatMap((id) => labelsById.get(id) ?? [])}
                assigneeNames={card.assigneeIds.flatMap(
                  (id) => membersById.get(id)?.displayName ?? [],
                )}
                readOnly={readOnly}
                onOpen={onOpenCard}
              />
            ))}
          </ul>
        </SortableContext>
      </div>

      {readOnly ? null : (
        <footer className="px-2 pb-2">
          {adding ? (
            <InlineTitleForm
              label="Card title"
              submitLabel="Add card"
              maxLength={200}
              multiline
              keepOpen
              onCancel={() => setAdding(false)}
              onSubmit={(title) =>
                createCard.mutateAsync({
                  columnId: column.id,
                  title,
                  description: null,
                  dueDate: null,
                })
              }
            />
          ) : (
            <Button
              variant="ghost"
              size="sm"
              className="w-full justify-start text-muted"
              onClick={() => setAdding(true)}
            >
              <Plus className="size-4" aria-hidden="true" />
              Add a card
            </Button>
          )}
        </footer>
      )}

      <ConfirmDialog
        open={deleting}
        title="Delete column"
        description={`"${column.title}" and its ${cards.length} ${cards.length === 1 ? 'card' : 'cards'} will be deleted.`}
        confirmLabel="Delete column"
        destructive
        loading={deleteColumn.isPending}
        onCancel={() => setDeleting(false)}
        onConfirm={() =>
          deleteColumn.mutate(column.id, {
            onSuccess: () => {
              toast.success('Column deleted');
              setDeleting(false);
            },
            onError: (error) => toast.error('Could not delete the column', error.message),
          })
        }
      />
    </section>
  );
}

/** The column as shown while it is being dragged. */
export function ColumnPreview({ column, cardCount }: { column: ColumnDto; cardCount: number }) {
  return (
    <div className="w-[85vw] rounded-lg bg-surface-muted p-3 shadow-popover sm:w-72">
      <p className="text-sm font-semibold">
        {column.title} <span className="font-normal text-muted">({cardCount})</span>
      </p>
    </div>
  );
}

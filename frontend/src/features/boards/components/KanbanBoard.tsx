import {
  closestCenter,
  closestCorners,
  DndContext,
  DragOverlay,
  KeyboardSensor,
  PointerSensor,
  pointerWithin,
  TouchSensor,
  useSensor,
  useSensors,
  type Announcements,
  type CollisionDetection,
  type DragEndEvent,
  type DragOverEvent,
  type DragStartEvent,
} from '@dnd-kit/core';
import {
  horizontalListSortingStrategy,
  SortableContext,
  sortableKeyboardCoordinates,
} from '@dnd-kit/sortable';
import { useEffect, useMemo, useState } from 'react';
import { groupCardsByColumn } from '@/features/cards/lib/board-state';
import { useMoveCard } from '@/features/cards/hooks/useMoveCard';
import type { CardDto, ColumnDto, LabelDto, WorkspaceMemberDto } from '@/shared/api/types';
import { compareByPosition } from '@/shared/lib/position';
import {
  arrayMove,
  findContainer,
  isSameSlot,
  moveBetweenContainers,
  slotOf,
  type Containers,
} from '../lib/dnd-state';
import { parseDragData } from '../lib/drag-data';
import { useMoveColumn } from '../hooks/useMoveColumn';
import { AddColumnForm } from './AddColumnForm';
import { CardBody } from './CardItem';
import { ColumnPreview, ColumnView } from './ColumnView';

interface KanbanBoardProps {
  boardId: string;
  columns: ColumnDto[];
  cards: CardDto[];
  labels: LabelDto[];
  members: WorkspaceMemberDto[];
  /** Archived boards, or a viewer without edit rights: no dragging, no composers. */
  readOnly: boolean;
  canDeleteColumn: boolean;
  onOpenCard: (cardId: string) => void;
}

const BODY_PREFIX = 'body:';

/** Column drags only consider columns; card drags prefer what the pointer is over, then the nearest corner. */
const collisionDetection: CollisionDetection = (args) => {
  if (parseDragData(args.active.data.current)?.type === 'column') {
    return closestCenter({
      ...args,
      droppableContainers: args.droppableContainers.filter(
        (container) => parseDragData(container.data.current)?.type === 'column',
      ),
    });
  }
  const within = pointerWithin(args);
  return within.length > 0 ? within : closestCorners(args);
};

/** Resolves any droppable id (card, column, column body) to the column it belongs to in `containers`. */
function columnOfDroppable(containers: Containers, id: string): string | undefined {
  return findContainer(containers, id.startsWith(BODY_PREFIX) ? id.slice(BODY_PREFIX.length) : id);
}

export function KanbanBoard({
  boardId,
  columns,
  cards,
  labels,
  members,
  readOnly,
  canDeleteColumn,
  onOpenCard,
}: KanbanBoardProps) {
  const moveCard = useMoveCard(boardId);
  const moveColumn = useMoveColumn(boardId);
  const [activeId, setActiveId] = useState<string | null>(null);
  const [draft, setDraft] = useState<Containers | null>(null);
  const [draftDone, setDraftDone] = useState(false);

  const sortedColumns = useMemo(() => [...columns].sort(compareByPosition), [columns]);
  const cardsById = useMemo(() => new Map(cards.map((card) => [card.id, card])), [cards]);
  const labelsById = useMemo(() => new Map(labels.map((label) => [label.id, label])), [labels]);
  const membersById = useMemo(
    () => new Map(members.map((member) => [member.userId, member])),
    [members],
  );

  const serverContainers = useMemo<Containers>(() => {
    const grouped = groupCardsByColumn(
      cards,
      sortedColumns.map((column) => column.id),
    );
    return Object.fromEntries(
      Object.entries(grouped).map(([columnId, list]) => [columnId, list.map((card) => card.id)]),
    );
  }, [cards, sortedColumns]);

  // After a drop the draft stays until the optimistic update (or rollback) changed the cache, so nothing flickers back.
  useEffect(() => {
    if (draftDone) {
      setDraft(null);
      setDraftDone(false);
    }
  }, [cards, draftDone]);

  const containers = draft ?? serverContainers;

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 6 } }),
    useSensor(TouchSensor, { activationConstraint: { delay: 250, tolerance: 8 } }),
    useSensor(KeyboardSensor, {
      coordinateGetter: sortableKeyboardCoordinates,
      keyboardCodes: { start: ['Space'], cancel: ['Escape'], end: ['Space', 'Enter'] },
    }),
  );

  const activeData = activeId === null ? undefined : cardsById.has(activeId) ? 'card' : 'column';
  const activeCard = activeId === null ? undefined : cardsById.get(activeId);
  const activeColumn =
    activeId === null ? undefined : sortedColumns.find((column) => column.id === activeId);

  function handleDragStart(event: DragStartEvent) {
    const id = String(event.active.id);
    setActiveId(id);
    if (parseDragData(event.active.data.current)?.type === 'card') {
      setDraft(serverContainers);
      setDraftDone(false);
    }
  }

  function handleDragOver({ active, over }: DragOverEvent) {
    if (over === null || parseDragData(active.data.current)?.type !== 'card') {
      return;
    }
    setDraft((current) => {
      if (current === null) {
        return current;
      }
      const activeKey = String(active.id);
      const overKey = String(over.id);
      const targetColumnId = columnOfDroppable(current, overKey);
      if (targetColumnId === undefined || targetColumnId === findContainer(current, activeKey)) {
        return current;
      }
      const overCard = overKey.startsWith(BODY_PREFIX) ? null : overKey;
      const activeRect = active.rect.current.translated;
      const placeBelow =
        overCard !== null &&
        activeRect !== null &&
        activeRect.top > over.rect.top + over.rect.height / 2;
      return moveBetweenContainers(current, activeKey, targetColumnId, overCard, placeBelow);
    });
  }

  function handleDragEnd({ active, over }: DragEndEvent) {
    const activeKey = String(active.id);
    const kind = parseDragData(active.data.current)?.type;
    setActiveId(null);

    if (kind === 'column') {
      if (over === null || over.id === active.id) {
        return;
      }
      const ids = sortedColumns.map((column) => column.id);
      const from = ids.indexOf(activeKey);
      const to = ids.indexOf(String(over.id));
      if (from < 0 || to < 0) {
        return;
      }
      const reordered = arrayMove(ids, from, to);
      const index = reordered.indexOf(activeKey);
      moveColumn.mutate({
        columnId: activeKey,
        afterId: reordered[index - 1] ?? null,
        beforeId: reordered[index + 1] ?? null,
      });
      return;
    }

    if (kind !== 'card' || draft === null) {
      return;
    }
    if (over === null) {
      setDraft(null); // dropped outside: cancel
      return;
    }

    let final = draft;
    const column = findContainer(draft, activeKey);
    const overKey = String(over.id);
    const order = column === undefined ? undefined : draft[column];
    if (
      column !== undefined &&
      order !== undefined &&
      !overKey.startsWith(BODY_PREFIX) &&
      overKey !== activeKey
    ) {
      const from = order.indexOf(activeKey);
      const to = order.indexOf(overKey);
      if (from >= 0 && to >= 0) {
        final = { ...draft, [column]: arrayMove(order, from, to) };
      }
    }

    const target = slotOf(final, activeKey);
    if (target === undefined || isSameSlot(target, slotOf(serverContainers, activeKey))) {
      setDraft(null);
      return;
    }

    setDraft(final);
    setDraftDone(true);
    moveCard.mutate({
      cardId: activeKey,
      targetColumnId: target.columnId,
      afterId: target.afterId,
      beforeId: target.beforeId,
    });
  }

  function handleDragCancel() {
    setActiveId(null);
    setDraft(null);
  }

  const announcements: Announcements = useMemo(() => {
    const describe = (id: string) => {
      const card = cardsById.get(id);
      return card
        ? `card ${card.title}`
        : `column ${sortedColumns.find((c) => c.id === id)?.title ?? ''}`;
    };
    const where = (overId: string | undefined) => {
      if (overId === undefined) {
        return 'outside the board';
      }
      const columnId = columnOfDroppable(containers, overId);
      const title = sortedColumns.find((c) => c.id === columnId)?.title;
      return title === undefined ? 'a new position' : `column ${title}`;
    };
    return {
      onDragStart: ({ active }) => `Picked up ${describe(String(active.id))}.`,
      onDragOver: ({ active, over }) =>
        over ? `${describe(String(active.id))} is over ${where(String(over.id))}.` : undefined,
      onDragEnd: ({ active, over }) =>
        over
          ? `Dropped ${describe(String(active.id))} in ${where(String(over.id))}.`
          : `${describe(String(active.id))} was dropped outside the board.`,
      onDragCancel: ({ active }) =>
        `Move cancelled. ${describe(String(active.id))} returned to its place.`,
    };
  }, [cardsById, sortedColumns, containers]);

  return (
    <DndContext
      sensors={sensors}
      collisionDetection={collisionDetection}
      accessibility={{
        announcements,
        screenReaderInstructions: {
          draggable:
            'To pick up, press Space. Use the arrow keys to move, Space or Enter to drop, Escape to cancel.',
        },
      }}
      onDragStart={handleDragStart}
      onDragOver={handleDragOver}
      onDragEnd={handleDragEnd}
      onDragCancel={handleDragCancel}
    >
      <div
        className="flex h-full snap-x snap-mandatory items-start gap-3 overflow-x-auto px-4 pb-4 sm:snap-none"
        data-testid="kanban"
      >
        <SortableContext
          items={sortedColumns.map((column) => column.id)}
          strategy={horizontalListSortingStrategy}
        >
          {sortedColumns.map((column) => (
            <ColumnView
              key={column.id}
              boardId={boardId}
              column={column}
              cards={(containers[column.id] ?? []).flatMap((id) => cardsById.get(id) ?? [])}
              labelsById={labelsById}
              membersById={membersById}
              readOnly={readOnly}
              canDelete={canDeleteColumn}
              onOpenCard={onOpenCard}
            />
          ))}
        </SortableContext>
        {readOnly ? null : <AddColumnForm boardId={boardId} />}
      </div>

      <DragOverlay>
        {activeData === 'card' && activeCard ? (
          <div className="rotate-2 cursor-grabbing rounded-md border border-primary bg-surface p-3 shadow-popover">
            <CardBody
              card={activeCard}
              labels={activeCard.labelIds.flatMap((id) => labelsById.get(id) ?? [])}
              assigneeNames={activeCard.assigneeIds.flatMap(
                (id) => membersById.get(id)?.displayName ?? [],
              )}
            />
          </div>
        ) : null}
        {activeData === 'column' && activeColumn ? (
          <ColumnPreview
            column={activeColumn}
            cardCount={containers[activeColumn.id]?.length ?? 0}
          />
        ) : null}
      </DragOverlay>
    </DndContext>
  );
}

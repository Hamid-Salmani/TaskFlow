/**
 * Pure helpers for the drag & drop state of the board. While a card is dragged, the Kanban keeps a local "draft" of
 * which card sits in which column; the server data is only touched when the card is dropped.
 */

/** Column id -> ids of its cards, in display order. */
export type Containers = Record<string, string[]>;

export function findContainer(containers: Containers, id: string): string | undefined {
  if (id in containers) {
    return id; // the id of a column itself
  }
  return Object.keys(containers).find((columnId) => containers[columnId]?.includes(id));
}

export function arrayMove<T>(items: readonly T[], from: number, to: number): T[] {
  const copy = [...items];
  const [moved] = copy.splice(from, 1);
  if (moved === undefined) {
    return copy;
  }
  copy.splice(Math.max(0, Math.min(to, copy.length)), 0, moved);
  return copy;
}

/**
 * Moves `activeId` into another column. It lands next to `overId` (below it when `placeBelow`), or at the end of the
 * column when the pointer is over the empty part of the column (`overId` is null or not a card of that column).
 */
export function moveBetweenContainers(
  containers: Containers,
  activeId: string,
  targetColumnId: string,
  overId: string | null,
  placeBelow: boolean,
): Containers {
  const sourceColumnId = findContainer(containers, activeId);
  const target = containers[targetColumnId];
  if (sourceColumnId === undefined || sourceColumnId === targetColumnId || target === undefined) {
    return containers;
  }

  const overIndex = overId === null ? -1 : target.indexOf(overId);
  const insertAt = overIndex < 0 ? target.length : overIndex + (placeBelow ? 1 : 0);

  return {
    ...containers,
    [sourceColumnId]: (containers[sourceColumnId] ?? []).filter((id) => id !== activeId),
    [targetColumnId]: [...target.slice(0, insertAt), activeId, ...target.slice(insertAt)],
  };
}

/** Result of dropping: the card's new column and the ids of the cards directly above and below it. */
export interface DropSlot {
  columnId: string;
  afterId: string | null;
  beforeId: string | null;
}

export function slotOf(containers: Containers, cardId: string): DropSlot | undefined {
  const columnId = findContainer(containers, cardId);
  const order = columnId === undefined ? undefined : containers[columnId];
  const index = order?.indexOf(cardId) ?? -1;
  if (columnId === undefined || order === undefined || index < 0) {
    return undefined;
  }
  return { columnId, afterId: order[index - 1] ?? null, beforeId: order[index + 1] ?? null };
}

/** True when the card sits where the server says it does, so no request is needed. */
export function isSameSlot(a: DropSlot | undefined, b: DropSlot | undefined): boolean {
  return (
    a !== undefined &&
    b !== undefined &&
    a.columnId === b.columnId &&
    a.afterId === b.afterId &&
    a.beforeId === b.beforeId
  );
}

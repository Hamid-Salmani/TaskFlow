/** What each draggable/droppable attaches as dnd-kit `data`, so handlers can tell cards from columns. */
export type DragData =
  | { type: 'card'; columnId: string }
  | { type: 'column' }
  | { type: 'column-body'; columnId: string };

export function parseDragData(value: unknown): DragData | undefined {
  if (typeof value !== 'object' || value === null || !('type' in value)) {
    return undefined;
  }

  const columnId =
    'columnId' in value && typeof value.columnId === 'string' ? value.columnId : undefined;
  switch (value.type) {
    case 'column':
      return { type: 'column' };
    case 'card':
      return columnId === undefined ? undefined : { type: 'card', columnId };
    case 'column-body':
      return columnId === undefined ? undefined : { type: 'column-body', columnId };
    default:
      return undefined;
  }
}

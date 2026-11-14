import type {
  BoardDetailDto,
  CardDto,
  ColumnDto,
  MoveCardResultDto,
  MoveColumnResultDto,
} from '@/shared/api/types';
import { planPlacement, sortByPosition, type PlacementError } from '@/shared/lib/position';

/**
 * Pure transformations of the cached board data. They are used by the optimistic updates (before the server
 * answers) and by the realtime handler (when another user's change arrives), so both paths behave identically.
 */

export type CardsByColumn = Record<string, CardDto[]>;

/** Cards grouped by column, every group ordered like the API orders them. Columns without cards get an empty list. */
export function groupCardsByColumn(
  cards: readonly CardDto[],
  columnIds: readonly string[],
): CardsByColumn {
  const groups: CardsByColumn = Object.fromEntries(columnIds.map((id) => [id, []]));
  for (const card of cards) {
    groups[card.columnId]?.push(card);
  }
  for (const id of columnIds) {
    groups[id] = sortByPosition(groups[id] ?? []);
  }
  return groups;
}

export function findCard(cards: readonly CardDto[], cardId: string): CardDto | undefined {
  return cards.find((card) => card.id === cardId);
}

export function upsertCard(cards: readonly CardDto[], card: CardDto): CardDto[] {
  const exists = cards.some((existing) => existing.id === card.id);
  return exists
    ? cards.map((existing) => (existing.id === card.id ? card : existing))
    : [...cards, card];
}

export function removeCard(cards: readonly CardDto[], cardId: string): CardDto[] {
  return cards.filter((card) => card.id !== cardId);
}

export interface CardMove {
  cardId: string;
  targetColumnId: string;
  afterId: string | null;
  beforeId: string | null;
}

export type MoveOutcome =
  { ok: true; cards: CardDto[] } | { ok: false; error: PlacementError | 'card_not_found' };

/**
 * Predicts the result of a move: puts the card into the target column at the slot between `afterId` and `beforeId`
 * and renumbers siblings when the position gap is used up. The version is deliberately NOT bumped: the next request
 * for this card must still carry the version the server knows, and the server's response sets the new one.
 */
export function applyCardMove(cards: readonly CardDto[], move: CardMove): MoveOutcome {
  const moving = findCard(cards, move.cardId);
  if (moving === undefined) {
    return { ok: false, error: 'card_not_found' };
  }

  const siblings = sortByPosition(
    cards.filter((card) => card.columnId === move.targetColumnId && card.id !== move.cardId),
  );
  const outcome = planPlacement(siblings, move.afterId, move.beforeId);
  if (!outcome.ok) {
    return outcome;
  }

  const { placement } = outcome;
  return {
    ok: true,
    cards: cards.map((card) => {
      if (card.id === move.cardId) {
        return {
          ...card,
          columnId: move.targetColumnId,
          position: placement.position,
        };
      }
      const renumbered = placement.siblingPositions.get(card.id);
      return renumbered === undefined ? card : { ...card, position: renumbered };
    }),
  };
}

/** Overwrites the prediction with what the server decided. */
export function applyMoveResult(cards: readonly CardDto[], result: MoveCardResultDto): CardDto[] {
  return cards.map((card) =>
    card.id === result.id
      ? { ...card, columnId: result.columnId, position: result.position, version: result.version }
      : card,
  );
}

export interface ColumnMove {
  columnId: string;
  afterId: string | null;
  beforeId: string | null;
}

export type ColumnMoveOutcome =
  { ok: true; board: BoardDetailDto } | { ok: false; error: PlacementError | 'column_not_found' };

export function applyColumnMove(board: BoardDetailDto, move: ColumnMove): ColumnMoveOutcome {
  if (!board.columns.some((column) => column.id === move.columnId)) {
    return { ok: false, error: 'column_not_found' };
  }

  const siblings = sortByPosition(board.columns.filter((column) => column.id !== move.columnId));
  const outcome = planPlacement(siblings, move.afterId, move.beforeId);
  if (!outcome.ok) {
    return outcome;
  }

  const { placement } = outcome;
  const columns: ColumnDto[] = board.columns.map((column) => {
    if (column.id === move.columnId) {
      return { ...column, position: placement.position };
    }
    const renumbered = placement.siblingPositions.get(column.id);
    return renumbered === undefined ? column : { ...column, position: renumbered };
  });
  return { ok: true, board: { ...board, columns: sortByPosition(columns) } };
}

export function applyColumnMoveResult(
  board: BoardDetailDto,
  result: MoveColumnResultDto,
): BoardDetailDto {
  const columns = board.columns.map((column) =>
    column.id === result.id ? { ...column, position: result.position } : column,
  );
  return { ...board, columns: sortByPosition(columns) };
}

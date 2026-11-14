/**
 * Fractional positions for ordered lists (cards in a column, columns in a board).
 *
 * This is the client-side twin of `TaskFlow.Application.Ordering.PositionService` / `PositionPlanner`: the same
 * algorithm lets the UI predict the position the server will assign, so an optimistic update lands in the right
 * place. The server stays authoritative; its response overwrites the prediction. See NOTES.md ("Position format").
 */

export const POSITION_STEP = 1024;
export const MIN_POSITION_GAP = 1e-6;

export interface Positioned {
  id: string;
  position: number;
}

/** Order used everywhere: by position, ties broken by id (ordinal), exactly like the API. */
export function compareByPosition(a: Positioned, b: Positioned): number {
  if (a.position !== b.position) {
    return a.position - b.position;
  }
  if (a.id < b.id) {
    return -1;
  }
  return a.id > b.id ? 1 : 0;
}

export function sortByPosition<T extends Positioned>(items: readonly T[]): T[] {
  return [...items].sort(compareByPosition);
}

export interface PositionBetween {
  position: number;
  /** The gap is too small to keep halving; the container has to be renumbered. */
  rebalanceRequired: boolean;
}

/**
 * Position for an item between two neighbours. `null` means "no neighbour on that side".
 * Callers guarantee `after < before`; `planPlacement` handles equal or inverted neighbours.
 */
export function positionBetween(after: number | null, before: number | null): PositionBetween {
  if (after === null && before === null) {
    return { position: POSITION_STEP, rebalanceRequired: false };
  }
  if (before === null) {
    return { position: (after ?? 0) + POSITION_STEP, rebalanceRequired: false };
  }
  if (after === null) {
    return { position: before - POSITION_STEP, rebalanceRequired: false };
  }

  const gap = before - after;
  return { position: after + gap / 2, rebalanceRequired: gap < MIN_POSITION_GAP };
}

/** Evenly spaced positions `1024, 2048, ...` for `count` items. */
export function rebalancedPositions(count: number): number[] {
  return Array.from({ length: count }, (_, index) => (index + 1) * POSITION_STEP);
}

export interface Placement {
  position: number;
  rebalanced: boolean;
  /** Only when `rebalanced`: the new position of every sibling, by id. */
  siblingPositions: ReadonlyMap<string, number>;
}

export type PlacementError = 'neighbor_not_found' | 'neighbors_out_of_order';

export type PlacementOutcome =
  { ok: true; placement: Placement } | { ok: false; error: PlacementError };

const NO_SIBLING_CHANGES: ReadonlyMap<string, number> = new Map();

/**
 * Translates "put it after X / before Y" into a position.
 *
 * `siblings` must be ordered and must NOT contain the item being placed. With neither id the item goes to the end;
 * with one id the other neighbour is derived from the current order.
 */
export function planPlacement(
  siblings: readonly Positioned[],
  afterId: string | null,
  beforeId: string | null,
): PlacementOutcome {
  const afterIndex = afterId === null ? -1 : siblings.findIndex((item) => item.id === afterId);
  const beforeIndex = beforeId === null ? -1 : siblings.findIndex((item) => item.id === beforeId);

  if ((afterId !== null && afterIndex < 0) || (beforeId !== null && beforeIndex < 0)) {
    return { ok: false, error: 'neighbor_not_found' };
  }
  if (afterIndex >= 0 && beforeIndex >= 0 && afterIndex >= beforeIndex) {
    return { ok: false, error: 'neighbors_out_of_order' };
  }

  let lowerIndex: number;
  let upperIndex: number;
  if (afterId === null && beforeId === null) {
    lowerIndex = siblings.length - 1; // append
    upperIndex = -1;
  } else {
    lowerIndex = afterIndex >= 0 ? afterIndex : beforeIndex - 1;
    upperIndex = beforeIndex >= 0 ? beforeIndex : afterIndex + 1;
  }
  const hasUpper = upperIndex >= 0 && upperIndex < siblings.length;
  const hasLower = lowerIndex >= 0;

  const lower = hasLower ? (siblings[lowerIndex]?.position ?? null) : null;
  const upper = hasUpper ? (siblings[upperIndex]?.position ?? null) : null;

  // Equal positions (two concurrent appends) are repaired by renumbering instead of failing, like the server.
  const collide = lower !== null && upper !== null && lower >= upper;
  const first = collide ? { position: 0, rebalanceRequired: true } : positionBetween(lower, upper);
  if (!first.rebalanceRequired) {
    return {
      ok: true,
      placement: {
        position: first.position,
        rebalanced: false,
        siblingPositions: NO_SIBLING_CHANGES,
      },
    };
  }

  const fresh = rebalancedPositions(siblings.length);
  const siblingPositions = new Map(
    siblings.map((item, index) => [item.id, fresh[index] ?? item.position]),
  );
  const second = positionBetween(
    hasLower ? (fresh[lowerIndex] ?? null) : null,
    hasUpper ? (fresh[upperIndex] ?? null) : null,
  );
  return { ok: true, placement: { position: second.position, rebalanced: true, siblingPositions } };
}

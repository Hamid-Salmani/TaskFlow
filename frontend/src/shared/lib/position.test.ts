import {
  compareByPosition,
  MIN_POSITION_GAP,
  planPlacement,
  POSITION_STEP,
  positionBetween,
  rebalancedPositions,
  sortByPosition,
  type Placement,
  type Positioned,
} from './position';

function items(...positions: number[]): Positioned[] {
  return positions.map((position, index) => ({ id: `id${index}`, position }));
}

function placed(
  siblings: readonly Positioned[],
  afterId: string | null,
  beforeId: string | null,
): Placement {
  const outcome = planPlacement(siblings, afterId, beforeId);
  if (!outcome.ok) {
    throw new Error(`Expected a placement but got ${outcome.error}`);
  }
  return outcome.placement;
}

describe('positionBetween', () => {
  it('uses the first slot for an empty list', () => {
    expect(positionBetween(null, null)).toEqual({
      position: POSITION_STEP,
      rebalanceRequired: false,
    });
  });

  it('appends one step after the last item', () => {
    expect(positionBetween(3000, null).position).toBe(3000 + POSITION_STEP);
  });

  it('prepends one step before the first item, going negative when needed', () => {
    expect(positionBetween(null, 1024).position).toBe(0);
    expect(positionBetween(null, 0).position).toBe(-POSITION_STEP);
  });

  it('takes the midpoint between neighbours', () => {
    expect(positionBetween(1000, 2000)).toEqual({ position: 1500, rebalanceRequired: false });
    expect(positionBetween(-10, 10).position).toBe(0);
  });

  it('asks for a rebalance once the gap is below the minimum, but not exactly at it', () => {
    expect(positionBetween(1, 1 + MIN_POSITION_GAP / 2).rebalanceRequired).toBe(true);
    expect(positionBetween(1, 2).rebalanceRequired).toBe(false);
    expect(positionBetween(0, MIN_POSITION_GAP).rebalanceRequired).toBe(false);
  });

  it('keeps halving without a rebalance for a long time (about 30 inserts from a 1024 gap)', () => {
    let upper = 1024;
    let inserts = 0;
    while (!positionBetween(0, upper).rebalanceRequired) {
      upper = positionBetween(0, upper).position;
      inserts += 1;
    }
    expect(inserts).toBeGreaterThanOrEqual(29);
    expect(inserts).toBeLessThanOrEqual(31);
  });
});

describe('rebalancedPositions', () => {
  it('numbers items 1024, 2048, ...', () => {
    expect(rebalancedPositions(3)).toEqual([1024, 2048, 3072]);
    expect(rebalancedPositions(0)).toEqual([]);
  });
});

describe('compareByPosition / sortByPosition', () => {
  it('orders by position and breaks ties by id, like the API', () => {
    const list: Positioned[] = [
      { id: 'b', position: 2 },
      { id: 'c', position: 1 },
      { id: 'a', position: 2 },
    ];

    expect(sortByPosition(list).map((item) => item.id)).toEqual(['c', 'a', 'b']);
    expect(compareByPosition({ id: 'a', position: 1 }, { id: 'a', position: 1 })).toBe(0);
  });

  it('does not mutate its input', () => {
    const list = items(3, 1, 2);
    sortByPosition(list);
    expect(list.map((item) => item.position)).toEqual([3, 1, 2]);
  });
});

describe('planPlacement', () => {
  const list = items(1000, 2000, 3000);

  it('appends when no neighbour is given, also into an empty list', () => {
    expect(placed(list, null, null).position).toBe(4024);
    expect(placed([], null, null).position).toBe(POSITION_STEP);
  });

  it('places between two neighbours', () => {
    expect(placed(list, 'id0', 'id1').position).toBe(1500);
  });

  it('with only beforeId places directly before that item, or at the start for the first item', () => {
    expect(placed(list, null, 'id2').position).toBe(2500);
    expect(placed(list, null, 'id0').position).toBe(1000 - POSITION_STEP);
  });

  it('with only afterId places directly after that item, or at the end for the last item', () => {
    expect(placed(list, 'id0', null).position).toBe(1500);
    expect(placed(list, 'id2', null).position).toBe(4024);
  });

  it('reports no sibling changes unless it rebalanced', () => {
    const placement = placed(list, 'id0', 'id1');

    expect(placement.rebalanced).toBe(false);
    expect(placement.siblingPositions.size).toBe(0);
  });

  it('rejects unknown neighbours', () => {
    expect(planPlacement(list, 'nope', null)).toEqual({ ok: false, error: 'neighbor_not_found' });
    expect(planPlacement(list, null, 'nope')).toEqual({ ok: false, error: 'neighbor_not_found' });
    expect(planPlacement(list, 'id0', 'nope')).toEqual({ ok: false, error: 'neighbor_not_found' });
  });

  it('rejects neighbours in the wrong order, and the same item twice', () => {
    expect(planPlacement(list, 'id2', 'id0')).toEqual({
      ok: false,
      error: 'neighbors_out_of_order',
    });
    expect(planPlacement(list, 'id1', 'id1')).toEqual({
      ok: false,
      error: 'neighbors_out_of_order',
    });
  });

  it('rebalances siblings when the gap is exhausted and still lands between the neighbours', () => {
    const tight = items(1, 1 + 1e-9, 5000);

    const placement = placed(tight, 'id0', 'id1');

    expect(placement.rebalanced).toBe(true);
    expect([...placement.siblingPositions]).toEqual([
      ['id0', 1024],
      ['id1', 2048],
      ['id2', 3072],
    ]);
    expect(placement.position).toBe(1536);
  });

  it('repairs equal neighbour positions (concurrent appends) instead of failing', () => {
    const tied = items(1024, 2048, 2048, 4096);

    const placement = placed(tied, 'id1', 'id2');

    expect(placement.rebalanced).toBe(true);
    expect(placement.position).toBeGreaterThan(
      placement.siblingPositions.get('id1') ?? Infinity - 1,
    );
    expect(placement.position).toBeLessThan(placement.siblingPositions.get('id2') ?? -Infinity);
    expect(new Set(placement.siblingPositions.values()).size).toBe(4);
  });

  it('rebalances for appends after an item that shares its position with the next one', () => {
    const placement = placed(items(1000, 1000), 'id0', null);

    expect(placement.rebalanced).toBe(true);
    expect(placement.position).toBe(1536);
  });

  it('survives many inserts right below the first item and always keeps a strict order (property check)', () => {
    let order: Positioned[] = items(1024, 2048);
    const inserted: string[] = [];

    for (let index = 0; index < 120; index += 1) {
      // Callers always pass adjacent neighbours: the first item and whatever currently follows it.
      const next = order[1];
      const outcome = planPlacement(order, 'id0', next?.id ?? null);
      if (!outcome.ok) {
        throw new Error(outcome.error);
      }
      const { placement } = outcome;
      order = order.map((item) => ({
        ...item,
        position: placement.siblingPositions.get(item.id) ?? item.position,
      }));
      const id = `new${index}`;
      order = sortByPosition([...order, { id, position: placement.position }]);
      inserted.unshift(id);

      const positions = order.map((item) => item.position);
      expect(new Set(positions).size).toBe(positions.length);
    }

    expect(order.map((item) => item.id)).toEqual(['id0', ...inserted, 'id1']);
  });
});

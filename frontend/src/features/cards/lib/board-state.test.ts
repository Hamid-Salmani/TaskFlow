import { makeBoard, makeCard } from '@/test/utils';
import {
  applyCardMove,
  applyColumnMove,
  applyColumnMoveResult,
  applyMoveResult,
  groupCardsByColumn,
  removeCard,
  upsertCard,
} from './board-state';

const a = makeCard({ id: 'a', columnId: 'col-1', position: 1000 });
const b = makeCard({ id: 'b', columnId: 'col-1', position: 2000 });
const c = makeCard({ id: 'c', columnId: 'col-2', position: 1000 });

function movedCards(result: ReturnType<typeof applyCardMove>) {
  if (!result.ok) {
    throw new Error(result.error);
  }
  return result.cards;
}

describe('groupCardsByColumn', () => {
  it('groups, orders by position (then id) and keeps empty columns', () => {
    const tied = makeCard({ id: 'z', columnId: 'col-1', position: 1000 });

    const groups = groupCardsByColumn([b, tied, a, c], ['col-1', 'col-2', 'col-3']);

    expect(groups['col-1']?.map((card) => card.id)).toEqual(['a', 'z', 'b']);
    expect(groups['col-2']?.map((card) => card.id)).toEqual(['c']);
    expect(groups['col-3']).toEqual([]);
  });

  it('ignores cards of unknown columns', () => {
    expect(groupCardsByColumn([makeCard({ columnId: 'gone' })], ['col-1'])).toEqual({
      'col-1': [],
    });
  });
});

describe('upsertCard / removeCard', () => {
  it('replaces an existing card, appends a new one and removes by id', () => {
    const updated = { ...a, title: 'Changed' };

    expect(upsertCard([a, b], updated)).toEqual([updated, b]);
    expect(upsertCard([a], b)).toEqual([a, b]);
    expect(removeCard([a, b], 'a')).toEqual([b]);
    expect(removeCard([a, b], 'nope')).toEqual([a, b]);
  });
});

describe('applyCardMove', () => {
  it('reorders inside a column: new position between neighbours, version untouched', () => {
    const third = makeCard({ id: 'third', columnId: 'col-1', position: 3000 });

    const cards = movedCards(
      applyCardMove([a, b, third], {
        cardId: 'third',
        targetColumnId: 'col-1',
        afterId: 'a',
        beforeId: 'b',
      }),
    );

    expect(cards.find((card) => card.id === 'third')).toMatchObject({
      position: 1500,
      version: 1,
      columnId: 'col-1',
    });
    expect(groupCardsByColumn(cards, ['col-1'])['col-1']?.map((card) => card.id)).toEqual([
      'a',
      'third',
      'b',
    ]);
  });

  it('moves across columns, to the end when no neighbours are given', () => {
    const cards = movedCards(
      applyCardMove([a, b, c], {
        cardId: 'a',
        targetColumnId: 'col-2',
        afterId: null,
        beforeId: null,
      }),
    );

    expect(cards.find((card) => card.id === 'a')).toMatchObject({
      columnId: 'col-2',
      position: 2024,
      version: 1,
    });
  });

  it('does not touch other cards unless it has to rebalance', () => {
    const result = movedCards(
      applyCardMove([a, b, c], {
        cardId: 'a',
        targetColumnId: 'col-2',
        afterId: null,
        beforeId: 'c',
      }),
    );

    expect(result.find((card) => card.id === 'b')).toBe(b);
    expect(result.find((card) => card.id === 'c')).toBe(c);
  });

  it('renumbers the target column when the gap is used up', () => {
    const tightA = makeCard({ id: 'ta', columnId: 'col-2', position: 1 });
    const tightB = makeCard({ id: 'tb', columnId: 'col-2', position: 1 + 1e-9 });

    const cards = movedCards(
      applyCardMove([a, tightA, tightB], {
        cardId: 'a',
        targetColumnId: 'col-2',
        afterId: 'ta',
        beforeId: 'tb',
      }),
    );

    const column = groupCardsByColumn(cards, ['col-2'])['col-2'] ?? [];
    expect(column.map((card) => card.id)).toEqual(['ta', 'a', 'tb']);
    expect(new Set(column.map((card) => card.position)).size).toBe(3);
  });

  it('does not mutate its input', () => {
    const input = [a, b];
    applyCardMove(input, { cardId: 'a', targetColumnId: 'col-1', afterId: 'b', beforeId: null });

    expect(input).toEqual([a, b]);
    expect(a.version).toBe(1);
  });

  it('reports unknown cards and invalid neighbours', () => {
    expect(
      applyCardMove([a], { cardId: 'x', targetColumnId: 'col-1', afterId: null, beforeId: null }),
    ).toEqual({ ok: false, error: 'card_not_found' });
    expect(
      applyCardMove([a, b], {
        cardId: 'a',
        targetColumnId: 'col-1',
        afterId: 'nope',
        beforeId: null,
      }),
    ).toEqual({ ok: false, error: 'neighbor_not_found' });
    expect(
      applyCardMove([a, b, c], {
        cardId: 'a',
        targetColumnId: 'col-1',
        afterId: 'c',
        beforeId: null,
      }),
    ).toEqual({ ok: false, error: 'neighbor_not_found' });
  });
});

describe('applyMoveResult', () => {
  it('overwrites column, position and version of the moved card only', () => {
    const cards = applyMoveResult([a, b], {
      id: 'a',
      columnId: 'col-2',
      position: 77,
      version: 5,
      rebalanced: false,
    });

    expect(cards[0]).toMatchObject({ id: 'a', columnId: 'col-2', position: 77, version: 5 });
    expect(cards[1]).toBe(b);
  });
});

describe('column moves', () => {
  const board = makeBoard();

  it('reorders columns and keeps them sorted', () => {
    const outcome = applyColumnMove(board, { columnId: 'col-2', afterId: null, beforeId: 'col-1' });

    expect(outcome.ok && outcome.board.columns.map((column) => column.id)).toEqual([
      'col-2',
      'col-1',
    ]);
    expect(outcome.ok && outcome.board.columns[0]?.position).toBe(0);
  });

  it('rejects unknown columns and neighbours', () => {
    expect(applyColumnMove(board, { columnId: 'x', afterId: null, beforeId: null })).toEqual({
      ok: false,
      error: 'column_not_found',
    });
    expect(applyColumnMove(board, { columnId: 'col-1', afterId: 'x', beforeId: null })).toEqual({
      ok: false,
      error: 'neighbor_not_found',
    });
  });

  it('applies the server result', () => {
    const updated = applyColumnMoveResult(board, {
      id: 'col-1',
      position: 9999,
      rebalanced: false,
    });

    expect(updated.columns.map((column) => column.id)).toEqual(['col-2', 'col-1']);
  });
});

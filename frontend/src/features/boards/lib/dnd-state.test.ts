import {
  arrayMove,
  findContainer,
  isSameSlot,
  moveBetweenContainers,
  slotOf,
  type Containers,
} from './dnd-state';

const board = (): Containers => ({ todo: ['a', 'b', 'c'], doing: ['d'], done: [] });

describe('findContainer', () => {
  it('finds the column of a card, accepts a column id, and returns undefined for strangers', () => {
    expect(findContainer(board(), 'b')).toBe('todo');
    expect(findContainer(board(), 'done')).toBe('done');
    expect(findContainer(board(), 'zzz')).toBeUndefined();
  });
});

describe('arrayMove', () => {
  it('moves forward and backward without mutating', () => {
    const input = ['a', 'b', 'c', 'd'];

    expect(arrayMove(input, 0, 2)).toEqual(['b', 'c', 'a', 'd']);
    expect(arrayMove(input, 3, 0)).toEqual(['d', 'a', 'b', 'c']);
    expect(arrayMove(input, 1, 1)).toEqual(input);
    expect(input).toEqual(['a', 'b', 'c', 'd']);
  });

  it('clamps the target index and ignores a missing source', () => {
    expect(arrayMove(['a', 'b'], 0, 99)).toEqual(['b', 'a']);
    expect(arrayMove(['a', 'b'], 5, 0)).toEqual(['a', 'b']);
  });
});

describe('moveBetweenContainers', () => {
  it('inserts above or below the card under the pointer', () => {
    expect(moveBetweenContainers(board(), 'a', 'doing', 'd', false).doing).toEqual(['a', 'd']);
    expect(moveBetweenContainers(board(), 'a', 'doing', 'd', true).doing).toEqual(['d', 'a']);
  });

  it('removes the card from its old column', () => {
    expect(moveBetweenContainers(board(), 'b', 'doing', 'd', true).todo).toEqual(['a', 'c']);
  });

  it('drops into an empty column, or at the end when not over a card', () => {
    expect(moveBetweenContainers(board(), 'a', 'done', null, false).done).toEqual(['a']);
    expect(moveBetweenContainers(board(), 'a', 'doing', null, false).doing).toEqual(['d', 'a']);
    expect(moveBetweenContainers(board(), 'a', 'doing', 'not-in-doing', false).doing).toEqual([
      'd',
      'a',
    ]);
  });

  it('does nothing inside the same column or for unknown ids/columns', () => {
    const state = board();

    expect(moveBetweenContainers(state, 'a', 'todo', 'c', true)).toBe(state);
    expect(moveBetweenContainers(state, 'ghost', 'doing', null, false)).toBe(state);
    expect(moveBetweenContainers(state, 'a', 'missing', null, false)).toBe(state);
  });

  it('does not mutate the input', () => {
    const state = board();
    moveBetweenContainers(state, 'a', 'doing', 'd', false);

    expect(state).toEqual(board());
  });
});

describe('slotOf', () => {
  it('reports the neighbours of a card', () => {
    expect(slotOf(board(), 'b')).toEqual({ columnId: 'todo', afterId: 'a', beforeId: 'c' });
    expect(slotOf(board(), 'a')).toEqual({ columnId: 'todo', afterId: null, beforeId: 'b' });
    expect(slotOf(board(), 'c')).toEqual({ columnId: 'todo', afterId: 'b', beforeId: null });
    expect(slotOf(board(), 'd')).toEqual({ columnId: 'doing', afterId: null, beforeId: null });
  });

  it('is undefined for unknown cards', () => {
    expect(slotOf(board(), 'ghost')).toBeUndefined();
  });
});

describe('isSameSlot', () => {
  it('compares column and both neighbours', () => {
    const slot = { columnId: 'todo', afterId: 'a', beforeId: 'c' };

    expect(isSameSlot(slot, { ...slot })).toBe(true);
    expect(isSameSlot(slot, { ...slot, beforeId: null })).toBe(false);
    expect(isSameSlot(slot, { ...slot, columnId: 'doing' })).toBe(false);
    expect(isSameSlot(slot, undefined)).toBe(false);
  });
});

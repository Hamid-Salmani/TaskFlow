import { parseDragData } from './drag-data';

describe('parseDragData', () => {
  it('accepts the three shapes', () => {
    expect(parseDragData({ type: 'card', columnId: 'c1' })).toEqual({
      type: 'card',
      columnId: 'c1',
    });
    expect(parseDragData({ type: 'column-body', columnId: 'c1' })).toEqual({
      type: 'column-body',
      columnId: 'c1',
    });
    expect(parseDragData({ type: 'column' })).toEqual({ type: 'column' });
  });

  it.each([
    undefined,
    null,
    'card',
    42,
    {},
    { type: 'other' },
    { type: 'card' },
    { type: 'card', columnId: 7 },
  ])('rejects %j', (value) => {
    expect(parseDragData(value)).toBeUndefined();
  });
});

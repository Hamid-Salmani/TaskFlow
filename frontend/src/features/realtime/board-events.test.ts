import { parseBoardEvent } from './board-events';

const ids = { boardId: 'b1', actorUserId: 'u2' };

describe('parseBoardEvent', () => {
  it('parses a CardMoved payload', () => {
    const event = parseBoardEvent('CardMoved', {
      ...ids,
      cardId: 'c1',
      fromColumnId: 'a',
      toColumnId: 'b',
      position: 1536,
      version: 3,
      rebalanced: false,
    });

    expect(event).toMatchObject({ name: 'CardMoved', payload: { cardId: 'c1', version: 3 } });
  });

  it('parses CommentDeleted', () => {
    expect(parseBoardEvent('CommentDeleted', { ...ids, cardId: 'c1', commentId: 'k1' })?.name).toBe(
      'CommentDeleted',
    );
  });

  it('parses ColumnsChanged and CardDeleted', () => {
    expect(parseBoardEvent('ColumnsChanged', ids)?.name).toBe('ColumnsChanged');
    expect(parseBoardEvent('CardDeleted', { ...ids, cardId: 'c1', columnId: 'a' })?.name).toBe(
      'CardDeleted',
    );
  });

  it.each([
    ['CardMoved', { ...ids, cardId: 'c1' }],
    ['CardCreated', { ...ids, card: { id: 'c1' } }],
    ['CardDeleted', null],
    ['CommentAdded', 'nope'],
    ['ColumnsChanged', {}],
  ] as const)('drops a malformed %s payload', (name, payload) => {
    expect(parseBoardEvent(name, payload)).toBeUndefined();
  });
});

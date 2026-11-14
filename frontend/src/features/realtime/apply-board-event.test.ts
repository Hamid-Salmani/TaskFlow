import type { QueryClient } from '@tanstack/react-query';
import { EMPTY_FILTERS, type CardFilters } from '@/features/cards/filters';
import { boardKeys } from '@/features/workspaces/query-keys';
import type { CardDto } from '@/shared/api/types';
import { createTestQueryClient, makeCard } from '@/test/utils';
import { applyBoardEvent } from './apply-board-event';
import type { BoardEvent } from './board-events';

const BOARD = 'board-1';
const ME = 'me';
const OTHER = 'other';

let queryClient: QueryClient;
const key = boardKeys.cards(BOARD, EMPTY_FILTERS);

function seed(cards: CardDto[]) {
  queryClient.setQueryData(key, cards);
}
function cached(): CardDto[] {
  return queryClient.getQueryData<CardDto[]>(key) ?? [];
}

beforeEach(() => {
  queryClient = createTestQueryClient();
});

describe('applyBoardEvent', () => {
  it('ignores events caused by the current user', () => {
    seed([]);
    const event: BoardEvent = {
      name: 'CardCreated',
      payload: { boardId: BOARD, actorUserId: ME, card: makeCard({ id: 'x' }) },
    };

    applyBoardEvent(queryClient, event, ME);

    expect(cached()).toEqual([]);
  });

  it('adds a card created by someone else', () => {
    seed([makeCard({ id: 'a' })]);
    const card = makeCard({ id: 'b', title: 'From a teammate' });

    applyBoardEvent(
      queryClient,
      { name: 'CardCreated', payload: { boardId: BOARD, actorUserId: OTHER, card } },
      ME,
    );

    expect(cached().map((c) => c.id)).toEqual(['a', 'b']);
  });

  it('moves a card to the position and version the server reports', () => {
    seed([makeCard({ id: 'a', columnId: 'col-1', position: 1024, version: 1 })]);

    applyBoardEvent(
      queryClient,
      {
        name: 'CardMoved',
        payload: {
          boardId: BOARD,
          actorUserId: OTHER,
          cardId: 'a',
          fromColumnId: 'col-1',
          toColumnId: 'col-2',
          position: 512,
          version: 2,
          rebalanced: false,
        },
      },
      ME,
    );

    expect(cached()[0]).toMatchObject({ columnId: 'col-2', position: 512, version: 2 });
  });

  it('ignores a stale move (older version than the cache)', () => {
    seed([makeCard({ id: 'a', columnId: 'col-2', position: 900, version: 5 })]);

    applyBoardEvent(
      queryClient,
      {
        name: 'CardMoved',
        payload: {
          boardId: BOARD,
          actorUserId: OTHER,
          cardId: 'a',
          fromColumnId: 'col-1',
          toColumnId: 'col-1',
          position: 1,
          version: 4,
          rebalanced: false,
        },
      },
      ME,
    );

    expect(cached()[0]).toMatchObject({ columnId: 'col-2', position: 900, version: 5 });
  });

  it('refetches the lists after a rebalancing move, because siblings were renumbered', () => {
    seed([makeCard({ id: 'a', version: 1 })]);
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries');

    applyBoardEvent(
      queryClient,
      {
        name: 'CardMoved',
        payload: {
          boardId: BOARD,
          actorUserId: OTHER,
          cardId: 'a',
          fromColumnId: 'col-1',
          toColumnId: 'col-1',
          position: 2,
          version: 2,
          rebalanced: true,
        },
      },
      ME,
    );

    expect(invalidate).toHaveBeenCalledWith({ queryKey: boardKeys.cardsOf(BOARD) });
  });

  it('refetches when a move concerns a card the cache does not know', () => {
    seed([]);
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries');

    applyBoardEvent(
      queryClient,
      {
        name: 'CardMoved',
        payload: {
          boardId: BOARD,
          actorUserId: OTHER,
          cardId: 'ghost',
          fromColumnId: 'a',
          toColumnId: 'b',
          position: 1,
          version: 2,
          rebalanced: false,
        },
      },
      ME,
    );

    expect(invalidate).toHaveBeenCalledWith({ queryKey: boardKeys.cardsOf(BOARD) });
  });

  it('applies updates only when newer, and removes deleted cards', () => {
    seed([makeCard({ id: 'a', title: 'v2', version: 2 }), makeCard({ id: 'b' })]);

    applyBoardEvent(
      queryClient,
      {
        name: 'CardUpdated',
        payload: {
          boardId: BOARD,
          actorUserId: OTHER,
          card: makeCard({ id: 'a', title: 'old', version: 1 }),
        },
      },
      ME,
    );
    expect(cached()[0]?.title).toBe('v2');

    applyBoardEvent(
      queryClient,
      {
        name: 'CardUpdated',
        payload: {
          boardId: BOARD,
          actorUserId: OTHER,
          card: makeCard({ id: 'a', title: 'v3', version: 3 }),
        },
      },
      ME,
    );
    expect(cached()[0]?.title).toBe('v3');

    applyBoardEvent(
      queryClient,
      {
        name: 'CardDeleted',
        payload: { boardId: BOARD, actorUserId: OTHER, cardId: 'b', columnId: 'col-1' },
      },
      ME,
    );
    expect(cached().map((c) => c.id)).toEqual(['a']);
  });

  it('counts a new comment and refreshes the board when the columns change', () => {
    seed([makeCard({ id: 'a', commentCount: 1 })]);
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries');

    applyBoardEvent(
      queryClient,
      {
        name: 'CommentAdded',
        payload: { boardId: BOARD, actorUserId: OTHER, cardId: 'a', comment: { id: 'cm1' } },
      },
      ME,
    );
    expect(cached()[0]?.commentCount).toBe(2);

    applyBoardEvent(
      queryClient,
      { name: 'ColumnsChanged', payload: { boardId: BOARD, actorUserId: OTHER } },
      ME,
    );
    expect(invalidate).toHaveBeenCalledWith({ queryKey: boardKeys.detail(BOARD) });
  });

  it('lowers the comment count when a comment is deleted, never below zero', () => {
    seed([makeCard({ id: 'a', commentCount: 1 })]);
    const event: BoardEvent = {
      name: 'CommentDeleted',
      payload: { boardId: BOARD, actorUserId: OTHER, cardId: 'a', commentId: 'cm1' },
    };

    applyBoardEvent(queryClient, event, ME);
    applyBoardEvent(queryClient, event, ME);

    expect(cached()[0]?.commentCount).toBe(0);
  });

  it('does not push new cards into filtered lists (they refetch instead)', () => {
    const filters: CardFilters = { ...EMPTY_FILTERS, q: 'bug' };
    const filteredKey = boardKeys.cards(BOARD, filters);
    queryClient.setQueryData(filteredKey, []);
    seed([]);

    applyBoardEvent(
      queryClient,
      {
        name: 'CardCreated',
        payload: { boardId: BOARD, actorUserId: OTHER, card: makeCard({ id: 'n' }) },
      },
      ME,
    );

    expect(queryClient.getQueryState(filteredKey)?.isInvalidated).toBe(true);
    expect(queryClient.getQueryState(key)?.isInvalidated).toBe(false);
  });
});

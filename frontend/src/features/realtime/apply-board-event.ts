import type { QueryClient } from '@tanstack/react-query';
import { hasActiveFilters, type CardFilters } from '@/features/cards/filters';
import { findCard, removeCard, upsertCard } from '@/features/cards/lib/board-state';
import { boardKeys, cardKeys } from '@/features/workspaces/query-keys';
import type { CardDto } from '@/shared/api/types';
import type { BoardEvent } from './board-events';

/** The filters part of a card-list query key (`['boards', 'cards', boardId, filters]`). */
function filtersOfKey(key: readonly unknown[]): CardFilters | undefined {
  const candidate = key[3];
  return typeof candidate === 'object' && candidate !== null && 'q' in candidate
    ? (candidate as CardFilters) // key shape is owned by boardKeys.cards
    : undefined;
}

function updateCards(
  queryClient: QueryClient,
  boardId: string,
  update: (cards: CardDto[]) => CardDto[],
) {
  queryClient.setQueriesData<CardDto[]>({ queryKey: boardKeys.cardsOf(boardId) }, (cards) =>
    cards ? update(cards) : cards,
  );
}

/** Card lists that apply a filter cannot be patched safely (the card may now match or not), so they refetch. */
function refetchFilteredLists(queryClient: QueryClient, boardId: string) {
  void queryClient.invalidateQueries({
    queryKey: boardKeys.cardsOf(boardId),
    predicate: (query) => {
      const filters = filtersOfKey(query.queryKey);
      return filters !== undefined && hasActiveFilters(filters);
    },
  });
}

function refetchAllCards(queryClient: QueryClient, boardId: string) {
  void queryClient.invalidateQueries({ queryKey: boardKeys.cardsOf(boardId) });
}

/**
 * Applies another user's change to the Query cache. Uses the same pure transformations as the optimistic updates.
 * Events about the current user's own actions are ignored: the HTTP response already updated the cache.
 * Stale events (older version than the cache holds) are ignored too, so out-of-order delivery cannot roll data back.
 */
export function applyBoardEvent(
  queryClient: QueryClient,
  event: BoardEvent,
  currentUserId: string | null,
): void {
  const { boardId, actorUserId } = event.payload;
  if (actorUserId === currentUserId) {
    return;
  }

  switch (event.name) {
    case 'CardCreated': {
      updateCards(queryClient, boardId, (cards) => upsertCard(cards, event.payload.card));
      refetchFilteredLists(queryClient, boardId);
      return;
    }
    case 'CardUpdated': {
      const { card } = event.payload;
      updateCards(queryClient, boardId, (cards) => {
        const cached = findCard(cards, card.id);
        return cached !== undefined && cached.version >= card.version
          ? cards
          : upsertCard(cards, card);
      });
      refetchFilteredLists(queryClient, boardId);
      return;
    }
    case 'CardMoved': {
      const move = event.payload;
      let known = false;
      updateCards(queryClient, boardId, (cards) => {
        const cached = findCard(cards, move.cardId);
        if (cached === undefined) {
          return cards;
        }
        known = true;
        if (cached.version >= move.version) {
          return cards;
        }
        return cards.map((card) =>
          card.id === move.cardId
            ? { ...card, columnId: move.toColumnId, position: move.position, version: move.version }
            : card,
        );
      });
      // A rebalance renumbered the siblings (we only know the moved card); an unknown card must be fetched.
      if (move.rebalanced || !known) {
        refetchAllCards(queryClient, boardId);
      } else {
        refetchFilteredLists(queryClient, boardId);
      }
      return;
    }
    case 'CardDeleted': {
      updateCards(queryClient, boardId, (cards) => removeCard(cards, event.payload.cardId));
      return;
    }
    case 'CommentAdded': {
      const { cardId } = event.payload;
      updateCards(queryClient, boardId, (cards) =>
        cards.map((card) =>
          card.id === cardId ? { ...card, commentCount: card.commentCount + 1 } : card,
        ),
      );
      void queryClient.invalidateQueries({ queryKey: cardKeys.comments(cardId) });
      return;
    }
    case 'CommentDeleted': {
      const { cardId } = event.payload;
      updateCards(queryClient, boardId, (cards) =>
        cards.map((card) =>
          card.id === cardId ? { ...card, commentCount: Math.max(0, card.commentCount - 1) } : card,
        ),
      );
      void queryClient.invalidateQueries({ queryKey: cardKeys.comments(cardId) });
      return;
    }
    case 'ColumnsChanged': {
      void queryClient.invalidateQueries({ queryKey: boardKeys.detail(boardId) });
      refetchAllCards(queryClient, boardId); // deleting a column removes its cards
      return;
    }
  }
}

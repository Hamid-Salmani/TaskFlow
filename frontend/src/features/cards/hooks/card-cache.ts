import type { QueryClient } from '@tanstack/react-query';
import { boardKeys } from '@/features/workspaces/query-keys';
import type { CardDto } from '@/shared/api/types';
import { removeCard, upsertCard } from '../lib/board-state';

/** Writes the server's version of a card into every cached card list of the board. */
export function writeCard(queryClient: QueryClient, boardId: string, card: CardDto): void {
  queryClient.setQueriesData<CardDto[]>({ queryKey: boardKeys.cardsOf(boardId) }, (cards) =>
    cards ? upsertCard(cards, card) : cards,
  );
}

export function dropCard(queryClient: QueryClient, boardId: string, cardId: string): void {
  queryClient.setQueriesData<CardDto[]>({ queryKey: boardKeys.cardsOf(boardId) }, (cards) =>
    cards ? removeCard(cards, cardId) : cards,
  );
}

export function adjustCommentCount(
  queryClient: QueryClient,
  boardId: string,
  cardId: string,
  delta: number,
): void {
  queryClient.setQueriesData<CardDto[]>({ queryKey: boardKeys.cardsOf(boardId) }, (cards) =>
    cards?.map((card) =>
      card.id === cardId ? { ...card, commentCount: Math.max(0, card.commentCount + delta) } : card,
    ),
  );
}

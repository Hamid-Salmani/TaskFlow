import {
  useMutation,
  useQueryClient,
  type QueryClient,
  type QueryKey,
} from '@tanstack/react-query';
import { boardKeys } from '@/features/workspaces/query-keys';
import type { ApiError } from '@/shared/api/errors';
import { request } from '@/shared/api/request';
import type { CardDto, MoveCardResultDto } from '@/shared/api/types';
import { toast } from '@/shared/ui';
import { applyCardMove, applyMoveResult, findCard, type CardMove } from '../lib/board-state';

type Snapshot = Array<[QueryKey, CardDto[] | undefined]>;

interface MoveCardContext {
  snapshot: Snapshot;
}

/** The newest cached version of a card, searched across every cached filter variant. */
function latestVersion(
  queryClient: QueryClient,
  boardId: string,
  cardId: string,
): number | undefined {
  const lists = queryClient.getQueriesData<CardDto[]>({ queryKey: boardKeys.cardsOf(boardId) });
  return lists
    .map(([, cards]) => (cards ? findCard(cards, cardId)?.version : undefined))
    .reduce<number | undefined>(
      (max, version) => (version === undefined ? max : Math.max(max ?? 0, version)),
      undefined,
    );
}

/**
 * Moves a card with an optimistic update:
 *  1. `onMutate` predicts the result with the same algorithm as the server and updates every cached card list at once.
 *  2. On success the server's position/version replace the prediction (and a rebalance triggers a refetch, because
 *     the siblings' positions changed too).
 *  3. On failure the snapshot is restored; a 409 additionally refetches, because the card changed under us.
 * Moves of a board run one after another (`scope`) so the next request always carries the version the previous one produced.
 */
export function useMoveCard(boardId: string) {
  const queryClient = useQueryClient();
  const queryKey = boardKeys.cardsOf(boardId);

  return useMutation<MoveCardResultDto, ApiError, CardMove, MoveCardContext>({
    scope: { id: `board-moves:${boardId}` },
    mutationFn: (move) =>
      request<MoveCardResultDto>({
        method: 'PUT',
        url: `/cards/${move.cardId}/move`,
        data: {
          expectedVersion: latestVersion(queryClient, boardId, move.cardId) ?? 1,
          targetColumnId: move.targetColumnId,
          afterId: move.afterId,
          beforeId: move.beforeId,
        },
      }),
    onMutate: async (move) => {
      await queryClient.cancelQueries({ queryKey });
      const snapshot = queryClient.getQueriesData<CardDto[]>({ queryKey });
      queryClient.setQueriesData<CardDto[]>({ queryKey }, (cards) => {
        if (cards === undefined) {
          return cards;
        }
        const outcome = applyCardMove(cards, move);
        return outcome.ok ? outcome.cards : cards;
      });
      return { snapshot };
    },
    onError: (error, _move, context) => {
      for (const [key, cards] of context?.snapshot ?? []) {
        queryClient.setQueryData(key, cards);
      }
      if (error.isConflict) {
        toast.error(
          'Card was changed',
          'Someone else updated this card, so the board was refreshed. Try again.',
        );
        void queryClient.invalidateQueries({ queryKey });
      } else {
        toast.error('Could not move the card', error.message);
      }
    },
    onSuccess: (result) => {
      queryClient.setQueriesData<CardDto[]>({ queryKey }, (cards) =>
        cards ? applyMoveResult(cards, result) : cards,
      );
      if (result.rebalanced) {
        void queryClient.invalidateQueries({ queryKey });
      }
    },
  });
}

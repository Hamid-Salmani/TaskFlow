import { useMutation, useQueryClient } from '@tanstack/react-query';
import { boardKeys } from '@/features/workspaces/query-keys';
import type { ApiError } from '@/shared/api/errors';
import { request } from '@/shared/api/request';
import type { CardDto } from '@/shared/api/types';
import { toast } from '@/shared/ui';
import { dropCard, writeCard } from './card-cache';

export interface UpdateCardVariables {
  cardId: string;
  expectedVersion: number;
  title: string;
  description: string | null;
  dueDate: string | null;
}

/**
 * Card edits are version-checked. A 409 means somebody else changed the card first: the board is refetched and the
 * caller keeps the user's input so they can review and save again against the new version.
 */
function useVersionedCardMutation<TVariables extends { cardId: string }>(
  boardId: string,
  send: (variables: TVariables) => Promise<CardDto>,
) {
  const queryClient = useQueryClient();
  return useMutation<CardDto, ApiError, TVariables>({
    mutationFn: send,
    onSuccess: (card) => writeCard(queryClient, boardId, card),
    onError: (error) => {
      if (error.isConflict) {
        toast.error(
          'Card was changed',
          'Someone else updated this card. The latest version was loaded.',
        );
        void queryClient.invalidateQueries({ queryKey: boardKeys.cardsOf(boardId) });
      }
    },
  });
}

export function useUpdateCard(boardId: string) {
  return useVersionedCardMutation<UpdateCardVariables>(boardId, ({ cardId, ...body }) =>
    request<CardDto>({ method: 'PUT', url: `/cards/${cardId}`, data: body }),
  );
}

export function useSetCardLabels(boardId: string) {
  return useVersionedCardMutation<{ cardId: string; expectedVersion: number; labelIds: string[] }>(
    boardId,
    ({ cardId, ...body }) =>
      request<CardDto>({ method: 'PUT', url: `/cards/${cardId}/labels`, data: body }),
  );
}

export function useSetCardAssignees(boardId: string) {
  return useVersionedCardMutation<{ cardId: string; expectedVersion: number; userIds: string[] }>(
    boardId,
    ({ cardId, ...body }) =>
      request<CardDto>({ method: 'PUT', url: `/cards/${cardId}/assignees`, data: body }),
  );
}

export function useDeleteCard(boardId: string) {
  const queryClient = useQueryClient();
  return useMutation<void, ApiError, string>({
    mutationFn: (cardId) => request<void>({ method: 'DELETE', url: `/cards/${cardId}` }),
    onSuccess: (_result, cardId) => dropCard(queryClient, boardId, cardId),
  });
}

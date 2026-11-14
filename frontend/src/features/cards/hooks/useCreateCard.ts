import { useMutation, useQueryClient } from '@tanstack/react-query';
import { boardKeys } from '@/features/workspaces/query-keys';
import type { ApiError } from '@/shared/api/errors';
import { request } from '@/shared/api/request';
import type { CardDto, CreateCardRequest } from '@/shared/api/types';
import { upsertCard } from '../lib/board-state';

interface CreateCardVariables extends CreateCardRequest {
  columnId: string;
}

export function useCreateCard(boardId: string) {
  const queryClient = useQueryClient();
  return useMutation<CardDto, ApiError, CreateCardVariables>({
    mutationFn: ({ columnId, ...body }) =>
      request<CardDto>({ method: 'POST', url: `/columns/${columnId}/cards`, data: body }),
    onSuccess: (card) => {
      // Show it immediately in every cached list (the server DTO is authoritative); filters re-apply on the next fetch.
      queryClient.setQueriesData<CardDto[]>({ queryKey: boardKeys.cardsOf(boardId) }, (cards) =>
        cards ? upsertCard(cards, card) : cards,
      );
    },
  });
}

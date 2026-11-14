import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { cardKeys } from '@/features/workspaces/query-keys';
import type { ApiError } from '@/shared/api/errors';
import { request } from '@/shared/api/request';
import type { ActivityPage, CommentDto, CommentPage } from '@/shared/api/types';
import { adjustCommentCount } from './card-cache';

const PAGE_SIZE = 100;

export function useComments(cardId: string) {
  return useQuery({
    queryKey: cardKeys.comments(cardId),
    queryFn: () =>
      request<CommentPage>({
        method: 'GET',
        url: `/cards/${cardId}/comments`,
        params: { page: 1, pageSize: PAGE_SIZE },
      }),
  });
}

export function useCardActivity(cardId: string) {
  return useQuery({
    queryKey: cardKeys.activity(cardId),
    queryFn: () =>
      request<ActivityPage>({
        method: 'GET',
        url: `/cards/${cardId}/activity`,
        params: { page: 1, pageSize: 50 },
      }),
  });
}

export function useAddComment(boardId: string, cardId: string) {
  const queryClient = useQueryClient();
  return useMutation<CommentDto, ApiError, string>({
    mutationFn: (body) =>
      request<CommentDto>({ method: 'POST', url: `/cards/${cardId}/comments`, data: { body } }),
    onSuccess: () => {
      adjustCommentCount(queryClient, boardId, cardId, 1);
      void queryClient.invalidateQueries({ queryKey: cardKeys.comments(cardId) });
      void queryClient.invalidateQueries({ queryKey: cardKeys.activity(cardId) });
    },
  });
}

export function useUpdateComment(cardId: string) {
  const queryClient = useQueryClient();
  return useMutation<CommentDto, ApiError, { commentId: string; body: string }>({
    mutationFn: ({ commentId, body }) =>
      request<CommentDto>({ method: 'PUT', url: `/comments/${commentId}`, data: { body } }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: cardKeys.comments(cardId) }),
  });
}

export function useDeleteComment(boardId: string, cardId: string) {
  const queryClient = useQueryClient();
  return useMutation<void, ApiError, string>({
    mutationFn: (commentId) => request<void>({ method: 'DELETE', url: `/comments/${commentId}` }),
    onSuccess: () => {
      adjustCommentCount(queryClient, boardId, cardId, -1);
      return queryClient.invalidateQueries({ queryKey: cardKeys.comments(cardId) });
    },
  });
}

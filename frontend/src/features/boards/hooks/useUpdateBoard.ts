import { useMutation, useQueryClient } from '@tanstack/react-query';
import { boardKeys, workspaceKeys } from '@/features/workspaces/query-keys';
import type { ApiError } from '@/shared/api/errors';
import { request } from '@/shared/api/request';
import type { BoardSummaryDto, UpdateBoardRequest } from '@/shared/api/types';

export function useUpdateBoard(workspaceId: string, boardId: string) {
  const queryClient = useQueryClient();
  return useMutation<BoardSummaryDto, ApiError, UpdateBoardRequest>({
    mutationFn: (body) =>
      request<BoardSummaryDto>({ method: 'PUT', url: `/boards/${boardId}`, data: body }),
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: workspaceKeys.boardsOf(workspaceId) }),
        queryClient.invalidateQueries({ queryKey: boardKeys.detail(boardId) }),
      ]),
  });
}

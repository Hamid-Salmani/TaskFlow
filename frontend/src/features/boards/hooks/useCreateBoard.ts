import { useMutation, useQueryClient } from '@tanstack/react-query';
import { workspaceKeys } from '@/features/workspaces/query-keys';
import type { ApiError } from '@/shared/api/errors';
import { request } from '@/shared/api/request';
import type { BoardSummaryDto, CreateBoardRequest } from '@/shared/api/types';

export function useCreateBoard(workspaceId: string) {
  const queryClient = useQueryClient();
  return useMutation<BoardSummaryDto, ApiError, CreateBoardRequest>({
    mutationFn: (body) =>
      request<BoardSummaryDto>({
        method: 'POST',
        url: `/workspaces/${workspaceId}/boards`,
        data: body,
      }),
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: workspaceKeys.boardsOf(workspaceId) }),
        queryClient.invalidateQueries({ queryKey: workspaceKeys.list() }),
      ]),
  });
}

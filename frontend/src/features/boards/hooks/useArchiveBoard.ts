import { useMutation, useQueryClient } from '@tanstack/react-query';
import { boardKeys, workspaceKeys } from '@/features/workspaces/query-keys';
import type { ApiError } from '@/shared/api/errors';
import { request } from '@/shared/api/request';
import type { BoardSummaryDto } from '@/shared/api/types';

interface ArchiveBoardVariables {
  boardId: string;
  archived: boolean;
}

export function useArchiveBoard(workspaceId: string) {
  const queryClient = useQueryClient();
  return useMutation<BoardSummaryDto, ApiError, ArchiveBoardVariables>({
    mutationFn: ({ boardId, archived }) =>
      request<BoardSummaryDto>({
        method: 'PUT',
        url: `/boards/${boardId}/archive`,
        data: { archived },
      }),
    onSuccess: (_board, { boardId }) =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: workspaceKeys.boardsOf(workspaceId) }),
        queryClient.invalidateQueries({ queryKey: boardKeys.detail(boardId) }),
      ]),
  });
}

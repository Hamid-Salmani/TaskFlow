import { useMutation, useQueryClient } from '@tanstack/react-query';
import { boardKeys, workspaceKeys } from '@/features/workspaces/query-keys';
import type { ApiError } from '@/shared/api/errors';
import { request } from '@/shared/api/request';

export function useDeleteBoard(workspaceId: string) {
  const queryClient = useQueryClient();
  return useMutation<void, ApiError, string>({
    mutationFn: (boardId) => request<void>({ method: 'DELETE', url: `/boards/${boardId}` }),
    onSuccess: (_data, boardId) => {
      queryClient.removeQueries({ queryKey: boardKeys.detail(boardId) });
      return Promise.all([
        queryClient.invalidateQueries({ queryKey: workspaceKeys.boardsOf(workspaceId) }),
        queryClient.invalidateQueries({ queryKey: workspaceKeys.list() }),
      ]);
    },
  });
}

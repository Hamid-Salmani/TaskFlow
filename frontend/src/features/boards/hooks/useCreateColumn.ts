import { useMutation, useQueryClient } from '@tanstack/react-query';
import { boardKeys } from '@/features/workspaces/query-keys';
import type { ApiError } from '@/shared/api/errors';
import { request } from '@/shared/api/request';
import type { BoardDetailDto, ColumnDto } from '@/shared/api/types';
import { sortByPosition } from '@/shared/lib/position';

export function useCreateColumn(boardId: string) {
  const queryClient = useQueryClient();
  return useMutation<ColumnDto, ApiError, string>({
    mutationFn: (title) =>
      request<ColumnDto>({ method: 'POST', url: `/boards/${boardId}/columns`, data: { title } }),
    onSuccess: (column) => {
      queryClient.setQueryData<BoardDetailDto>(boardKeys.detail(boardId), (board) =>
        board && !board.columns.some((existing) => existing.id === column.id)
          ? { ...board, columns: sortByPosition([...board.columns, column]) }
          : board,
      );
    },
  });
}

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { boardKeys } from '@/features/workspaces/query-keys';
import type { ApiError } from '@/shared/api/errors';
import { request } from '@/shared/api/request';
import type { BoardDetailDto, ColumnDto } from '@/shared/api/types';

interface RenameColumnVariables {
  columnId: string;
  title: string;
}

export function useRenameColumn(boardId: string) {
  const queryClient = useQueryClient();
  return useMutation<ColumnDto, ApiError, RenameColumnVariables>({
    mutationFn: ({ columnId, title }) =>
      request<ColumnDto>({ method: 'PUT', url: `/columns/${columnId}`, data: { title } }),
    onSuccess: (column) => {
      queryClient.setQueryData<BoardDetailDto>(boardKeys.detail(boardId), (board) =>
        board
          ? {
              ...board,
              columns: board.columns.map((existing) =>
                existing.id === column.id ? column : existing,
              ),
            }
          : board,
      );
    },
  });
}

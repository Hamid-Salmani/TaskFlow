import { useMutation, useQueryClient } from '@tanstack/react-query';
import { boardKeys } from '@/features/workspaces/query-keys';
import type { ApiError } from '@/shared/api/errors';
import { request } from '@/shared/api/request';
import type { BoardDetailDto, CardDto } from '@/shared/api/types';

export function useDeleteColumn(boardId: string) {
  const queryClient = useQueryClient();
  return useMutation<void, ApiError, string>({
    mutationFn: (columnId) => request<void>({ method: 'DELETE', url: `/columns/${columnId}` }),
    onSuccess: (_data, columnId) => {
      queryClient.setQueryData<BoardDetailDto>(boardKeys.detail(boardId), (board) =>
        board
          ? { ...board, columns: board.columns.filter((column) => column.id !== columnId) }
          : board,
      );
      queryClient.setQueriesData<CardDto[]>({ queryKey: boardKeys.cardsOf(boardId) }, (cards) =>
        cards?.filter((card) => card.columnId !== columnId),
      );
    },
  });
}

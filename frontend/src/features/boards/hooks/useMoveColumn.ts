import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  applyColumnMove,
  applyColumnMoveResult,
  type ColumnMove,
} from '@/features/cards/lib/board-state';
import { boardKeys } from '@/features/workspaces/query-keys';
import type { ApiError } from '@/shared/api/errors';
import { request } from '@/shared/api/request';
import type { BoardDetailDto, MoveColumnResultDto } from '@/shared/api/types';
import { toast } from '@/shared/ui';

interface MoveColumnContext {
  previous: BoardDetailDto | undefined;
}

/** Reorders a column optimistically; rolls back and tells the user when the server refuses. */
export function useMoveColumn(boardId: string) {
  const queryClient = useQueryClient();
  const queryKey = boardKeys.detail(boardId);

  return useMutation<MoveColumnResultDto, ApiError, ColumnMove, MoveColumnContext>({
    scope: { id: `board-moves:${boardId}` },
    mutationFn: (move) =>
      request<MoveColumnResultDto>({
        method: 'PUT',
        url: `/columns/${move.columnId}/move`,
        data: { afterId: move.afterId, beforeId: move.beforeId },
      }),
    onMutate: async (move) => {
      await queryClient.cancelQueries({ queryKey });
      const previous = queryClient.getQueryData<BoardDetailDto>(queryKey);
      if (previous) {
        const outcome = applyColumnMove(previous, move);
        if (outcome.ok) {
          queryClient.setQueryData(queryKey, outcome.board);
        }
      }
      return { previous };
    },
    onError: (error, _move, context) => {
      queryClient.setQueryData(queryKey, context?.previous);
      toast.error('Could not move the column', error.message);
    },
    onSuccess: (result) => {
      queryClient.setQueryData<BoardDetailDto>(queryKey, (board) =>
        board ? applyColumnMoveResult(board, result) : board,
      );
      if (result.rebalanced) {
        void queryClient.invalidateQueries({ queryKey });
      }
    },
  });
}

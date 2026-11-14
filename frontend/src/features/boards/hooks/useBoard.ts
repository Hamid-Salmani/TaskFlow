import { useQuery } from '@tanstack/react-query';
import { boardKeys } from '@/features/workspaces/query-keys';
import { request } from '@/shared/api/request';
import type { BoardDetailDto } from '@/shared/api/types';

export function useBoard(boardId: string) {
  return useQuery({
    queryKey: boardKeys.detail(boardId),
    queryFn: () => request<BoardDetailDto>({ method: 'GET', url: `/boards/${boardId}` }),
  });
}

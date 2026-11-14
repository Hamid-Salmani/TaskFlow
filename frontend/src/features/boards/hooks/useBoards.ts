import { useQuery } from '@tanstack/react-query';
import { request } from '@/shared/api/request';
import type { BoardSummaryDto } from '@/shared/api/types';
import { workspaceKeys } from '@/features/workspaces/query-keys';

export function useBoards(workspaceId: string, includeArchived: boolean) {
  return useQuery({
    queryKey: workspaceKeys.boards(workspaceId, includeArchived),
    queryFn: () =>
      request<BoardSummaryDto[]>({
        method: 'GET',
        url: `/workspaces/${workspaceId}/boards`,
        params: { includeArchived },
      }),
  });
}

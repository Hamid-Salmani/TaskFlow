import { useQuery } from '@tanstack/react-query';
import { request } from '@/shared/api/request';
import type { WorkspaceMemberDto } from '@/shared/api/types';
import { workspaceKeys } from '../query-keys';

export function useWorkspaceMembers(workspaceId: string) {
  return useQuery({
    queryKey: workspaceKeys.members(workspaceId),
    queryFn: () =>
      request<WorkspaceMemberDto[]>({ method: 'GET', url: `/workspaces/${workspaceId}/members` }),
  });
}

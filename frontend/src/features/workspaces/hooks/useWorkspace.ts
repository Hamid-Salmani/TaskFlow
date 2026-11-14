import { useQuery } from '@tanstack/react-query';
import { request } from '@/shared/api/request';
import type { WorkspaceDto } from '@/shared/api/types';
import { workspaceKeys } from '../query-keys';

export function useWorkspace(workspaceId: string) {
  return useQuery({
    queryKey: workspaceKeys.detail(workspaceId),
    queryFn: () => request<WorkspaceDto>({ method: 'GET', url: `/workspaces/${workspaceId}` }),
  });
}

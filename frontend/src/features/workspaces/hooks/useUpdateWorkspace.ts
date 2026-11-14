import { useMutation, useQueryClient } from '@tanstack/react-query';
import type { ApiError } from '@/shared/api/errors';
import { request } from '@/shared/api/request';
import type { UpdateWorkspaceRequest, WorkspaceDto } from '@/shared/api/types';
import { workspaceKeys } from '../query-keys';

export function useUpdateWorkspace(workspaceId: string) {
  const queryClient = useQueryClient();
  return useMutation<WorkspaceDto, ApiError, UpdateWorkspaceRequest>({
    mutationFn: (body) =>
      request<WorkspaceDto>({ method: 'PUT', url: `/workspaces/${workspaceId}`, data: body }),
    onSuccess: (workspace) => {
      queryClient.setQueryData(workspaceKeys.detail(workspaceId), workspace);
      return queryClient.invalidateQueries({ queryKey: workspaceKeys.list() });
    },
  });
}

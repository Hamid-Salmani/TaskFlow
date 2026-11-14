import { useMutation, useQueryClient } from '@tanstack/react-query';
import type { ApiError } from '@/shared/api/errors';
import { request } from '@/shared/api/request';
import type { CreateWorkspaceRequest, WorkspaceDto } from '@/shared/api/types';
import { workspaceKeys } from '../query-keys';

export function useCreateWorkspace() {
  const queryClient = useQueryClient();
  return useMutation<WorkspaceDto, ApiError, CreateWorkspaceRequest>({
    mutationFn: (body) => request<WorkspaceDto>({ method: 'POST', url: '/workspaces', data: body }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: workspaceKeys.list() }),
  });
}

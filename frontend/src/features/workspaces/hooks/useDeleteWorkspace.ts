import { useMutation, useQueryClient } from '@tanstack/react-query';
import type { ApiError } from '@/shared/api/errors';
import { request } from '@/shared/api/request';
import { workspaceKeys } from '../query-keys';

export function useDeleteWorkspace(workspaceId: string) {
  const queryClient = useQueryClient();
  return useMutation<void, ApiError, void>({
    mutationFn: () => request<void>({ method: 'DELETE', url: `/workspaces/${workspaceId}` }),
    onSuccess: () => {
      queryClient.removeQueries({ queryKey: workspaceKeys.detail(workspaceId) });
      return queryClient.invalidateQueries({ queryKey: workspaceKeys.list() });
    },
  });
}

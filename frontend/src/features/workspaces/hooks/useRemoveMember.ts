import { useMutation, useQueryClient } from '@tanstack/react-query';
import type { ApiError } from '@/shared/api/errors';
import { request } from '@/shared/api/request';
import { workspaceKeys } from '../query-keys';

export function useRemoveMember(workspaceId: string) {
  const queryClient = useQueryClient();
  return useMutation<void, ApiError, string>({
    mutationFn: (userId) =>
      request<void>({ method: 'DELETE', url: `/workspaces/${workspaceId}/members/${userId}` }),
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: workspaceKeys.members(workspaceId) }),
        queryClient.invalidateQueries({ queryKey: workspaceKeys.list() }),
      ]),
  });
}

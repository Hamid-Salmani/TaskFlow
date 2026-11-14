import { useMutation, useQueryClient } from '@tanstack/react-query';
import type { ApiError } from '@/shared/api/errors';
import { request } from '@/shared/api/request';
import type { WorkspaceMemberDto, WorkspaceRole } from '@/shared/api/types';
import { workspaceKeys } from '../query-keys';

interface ChangeMemberRoleVariables {
  userId: string;
  role: WorkspaceRole;
}

export function useChangeMemberRole(workspaceId: string) {
  const queryClient = useQueryClient();
  return useMutation<WorkspaceMemberDto, ApiError, ChangeMemberRoleVariables>({
    mutationFn: ({ userId, role }) =>
      request<WorkspaceMemberDto>({
        method: 'PUT',
        url: `/workspaces/${workspaceId}/members/${userId}`,
        data: { role },
      }),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: workspaceKeys.members(workspaceId) }),
  });
}

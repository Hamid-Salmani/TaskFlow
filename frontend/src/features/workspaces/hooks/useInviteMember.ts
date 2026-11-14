import { useMutation, useQueryClient } from '@tanstack/react-query';
import type { ApiError } from '@/shared/api/errors';
import { request } from '@/shared/api/request';
import type { InviteMemberRequest, WorkspaceMemberDto } from '@/shared/api/types';
import { workspaceKeys } from '../query-keys';

export function useInviteMember(workspaceId: string) {
  const queryClient = useQueryClient();
  return useMutation<WorkspaceMemberDto, ApiError, InviteMemberRequest>({
    mutationFn: (body) =>
      request<WorkspaceMemberDto>({
        method: 'POST',
        url: `/workspaces/${workspaceId}/members`,
        data: body,
      }),
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: workspaceKeys.members(workspaceId) }),
        queryClient.invalidateQueries({ queryKey: workspaceKeys.detail(workspaceId) }),
      ]),
  });
}

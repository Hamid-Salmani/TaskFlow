import { useQuery } from '@tanstack/react-query';
import { api } from '@/shared/api/client';
import { toApiError } from '@/shared/api/errors';
import type { UserDto } from '@/shared/api/types';
import { selectIsAuthenticated, useSessionStore } from '@/shared/store/session-store';

export const currentUserKey = ['auth', 'me'] as const;

/** Validates the persisted session on startup (a stale token is refreshed by the client or signs the user out). */
export function useCurrentUser() {
  const enabled = useSessionStore(selectIsAuthenticated);

  return useQuery({
    queryKey: currentUserKey,
    enabled,
    queryFn: async (): Promise<UserDto> => {
      try {
        const { data } = await api.get<UserDto>('/auth/me');
        return data;
      } catch (error) {
        throw toApiError(error);
      }
    },
  });
}

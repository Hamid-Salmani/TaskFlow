import { useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/shared/api/client';
import { useSessionStore } from '@/shared/store/session-store';

/** Revokes the refresh token on the server (best effort) and always clears the local session. */
export function useLogout() {
  const queryClient = useQueryClient();
  const clear = useSessionStore((state) => state.clear);

  return useMutation({
    mutationFn: async () => {
      const refreshToken = useSessionStore.getState().session?.refreshToken;
      if (!refreshToken) {
        return;
      }

      try {
        await api.post('/auth/logout', { refreshToken }, { skipAuth: true });
      } catch {
        // The token expires on its own; signing out locally must not depend on the network.
      }
    },
    onSettled: () => {
      clear();
      queryClient.clear();
    },
  });
}

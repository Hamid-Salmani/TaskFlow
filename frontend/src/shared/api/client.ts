import { useSessionStore } from '@/shared/store/session-store';
import { toast } from '@/shared/ui/toast-store';
import { createApiClient } from './create-client';
import { queryClient } from './query-client';

export const API_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5080';
export const API_BASE_URL = `${API_URL}/api/v1`;

/** The application-wide client, wired to the session store. */
export const api = createApiClient({
  baseURL: API_BASE_URL,
  getAccessToken: () => useSessionStore.getState().session?.accessToken ?? null,
  getRefreshToken: () => useSessionStore.getState().session?.refreshToken ?? null,
  onTokensRefreshed: (auth) => useSessionStore.getState().setSession(auth),
  onAuthFailure: () => {
    if (useSessionStore.getState().session === null) {
      return;
    }

    useSessionStore.getState().clear();
    queryClient.clear();
    toast.info('Session expired', 'Please sign in again.');
  },
});

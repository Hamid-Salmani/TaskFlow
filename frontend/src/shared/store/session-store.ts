import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { AuthResponse, UserDto } from '@/shared/api/types';

export interface Session {
  accessToken: string;
  refreshToken: string;
  user: UserDto;
}

interface SessionState {
  /** `null` means anonymous. */
  session: Session | null;
  setSession: (auth: AuthResponse) => void;
  clear: () => void;
}

/**
 * Auth state. Lives in `shared` because the HTTP client, the SignalR connection and the route guards all read it.
 * Persisted so a page reload keeps the user signed in (see README for the localStorage trade-off).
 */
export const useSessionStore = create<SessionState>()(
  persist(
    (set) => ({
      session: null,
      setSession: (auth) =>
        set({
          session: {
            accessToken: auth.accessToken,
            refreshToken: auth.refreshToken,
            user: auth.user,
          },
        }),
      clear: () => set({ session: null }),
    }),
    { name: 'taskflow-session' },
  ),
);

export const selectUser = (state: SessionState): UserDto | null => state.session?.user ?? null;
export const selectIsAuthenticated = (state: SessionState): boolean => state.session !== null;

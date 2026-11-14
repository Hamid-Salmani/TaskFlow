import { makeAuthResponse, testUser } from '@/test/utils';
import { selectIsAuthenticated, selectUser, useSessionStore } from './session-store';

describe('session store', () => {
  beforeEach(() => useSessionStore.getState().clear());

  it('starts anonymous', () => {
    const state = useSessionStore.getState();

    expect(state.session).toBeNull();
    expect(selectIsAuthenticated(state)).toBe(false);
    expect(selectUser(state)).toBeNull();
  });

  it('stores tokens and user from an auth response and drops everything else', () => {
    useSessionStore.getState().setSession(makeAuthResponse());

    const state = useSessionStore.getState();
    expect(state.session).toEqual({
      accessToken: 'access-token',
      refreshToken: 'refresh-token',
      user: testUser,
    });
    expect(selectIsAuthenticated(state)).toBe(true);
    expect(selectUser(state)).toEqual(testUser);
  });

  it('replaces the session when tokens are rotated', () => {
    useSessionStore.getState().setSession(makeAuthResponse());
    useSessionStore
      .getState()
      .setSession(makeAuthResponse({ accessToken: 'a2', refreshToken: 'r2' }));

    expect(useSessionStore.getState().session).toMatchObject({
      accessToken: 'a2',
      refreshToken: 'r2',
    });
  });

  it('clears the session', () => {
    useSessionStore.getState().setSession(makeAuthResponse());

    useSessionStore.getState().clear();

    expect(useSessionStore.getState().session).toBeNull();
  });

  it('persists the session so a reload keeps the user signed in', () => {
    useSessionStore.getState().setSession(makeAuthResponse());

    const raw = localStorage.getItem('taskflow-session');
    expect(raw).not.toBeNull();
    expect(JSON.parse(raw ?? '{}')).toMatchObject({
      state: { session: { refreshToken: 'refresh-token' } },
    });
  });
});

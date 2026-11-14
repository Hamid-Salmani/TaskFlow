import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { selectIsAuthenticated, useSessionStore } from '@/shared/store/session-store';

/** Reads the `from` location that ProtectedRoute stores when it redirects to the login page. */
export function readRedirectTarget(state: unknown): string | null {
  if (typeof state === 'object' && state !== null && 'from' in state) {
    const { from } = state;
    return typeof from === 'string' && from.startsWith('/') ? from : null;
  }
  return null;
}

/** Renders the nested routes only for signed-in users; everyone else is sent to /login and brought back afterwards. */
export function ProtectedRoute() {
  const isAuthenticated = useSessionStore(selectIsAuthenticated);
  const location = useLocation();

  if (!isAuthenticated) {
    return (
      <Navigate to="/login" replace state={{ from: `${location.pathname}${location.search}` }} />
    );
  }

  return <Outlet />;
}

/** Login and register are pointless for signed-in users. */
export function GuestRoute() {
  const isAuthenticated = useSessionStore(selectIsAuthenticated);

  if (isAuthenticated) {
    return <Navigate to="/workspaces" replace />;
  }

  return <Outlet />;
}

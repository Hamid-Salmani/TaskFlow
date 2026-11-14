import { LayoutDashboard, LogOut, Moon, Settings, Sun } from 'lucide-react';
import { Suspense } from 'react';
import { Link, Outlet, useNavigate } from 'react-router-dom';
import { useCurrentUser } from '@/features/auth/hooks/useCurrentUser';
import { useLogout } from '@/features/auth/hooks/useLogout';
import { NotificationsMenu } from '@/features/notifications/components/NotificationsMenu';
import { resolveIsDark, useUiStore } from '@/shared/store/ui-store';
import { selectUser, useSessionStore } from '@/shared/store/session-store';
import { Avatar, Button, Dropdown, ErrorBoundary, Skeleton, type DropdownItem } from '@/shared/ui';

function ThemeToggle() {
  const theme = useUiStore((state) => state.theme);
  const setTheme = useUiStore((state) => state.setTheme);
  const isDark = resolveIsDark(theme, window.matchMedia('(prefers-color-scheme: dark)').matches);

  return (
    <Button
      variant="ghost"
      size="icon"
      aria-label={isDark ? 'Switch to light theme' : 'Switch to dark theme'}
      onClick={() => setTheme(isDark ? 'light' : 'dark')}
    >
      {isDark ? (
        <Sun className="size-4" aria-hidden="true" />
      ) : (
        <Moon className="size-4" aria-hidden="true" />
      )}
    </Button>
  );
}

function UserMenu() {
  const user = useSessionStore(selectUser);
  const navigate = useNavigate();
  const logout = useLogout();

  if (!user) {
    return null;
  }

  const items: DropdownItem[] = [
    {
      id: 'settings',
      label: 'Settings',
      icon: <Settings className="size-4" aria-hidden="true" />,
      onSelect: () => navigate('/settings'),
    },
    {
      id: 'logout',
      label: 'Sign out',
      icon: <LogOut className="size-4" aria-hidden="true" />,
      onSelect: () => logout.mutate(),
    },
  ];

  return (
    <Dropdown
      triggerLabel="Account menu"
      trigger={<Avatar name={user.displayName} />}
      triggerClassName="rounded-full p-0.5"
      header={
        <div className="min-w-0">
          <p className="truncate text-sm font-medium">{user.displayName}</p>
          <p className="truncate text-xs text-muted">{user.email}</p>
        </div>
      }
      items={items}
    />
  );
}

export function AppLayout() {
  // Validates the stored session on startup; an expired one is refreshed or ends in a sign-out.
  useCurrentUser();

  return (
    <div className="flex min-h-screen flex-col">
      <a
        href="#main"
        className="sr-only focus:not-sr-only focus:absolute focus:left-2 focus:top-2 focus:z-50 focus:rounded-md focus:bg-surface focus:px-3 focus:py-2"
      >
        Skip to content
      </a>
      <header className="sticky top-0 z-30 border-b border-border bg-surface/90 backdrop-blur">
        <div className="flex h-14 items-center justify-between gap-4 px-4 sm:px-6">
          <Link to="/workspaces" className="flex items-center gap-2 rounded-md font-semibold">
            <span className="flex size-8 items-center justify-center rounded-md bg-primary text-primary-foreground">
              <LayoutDashboard className="size-4" aria-hidden="true" />
            </span>
            <span>TaskFlow</span>
          </Link>
          <div className="flex items-center gap-1">
            <NotificationsMenu />
            <ThemeToggle />
            <UserMenu />
          </div>
        </div>
      </header>
      <main id="main" tabIndex={-1} className="flex min-h-0 flex-1 flex-col outline-none">
        <ErrorBoundary>
          <Suspense fallback={<PageFallback />}>
            <Outlet />
          </Suspense>
        </ErrorBoundary>
      </main>
    </div>
  );
}

export function PageFallback() {
  return (
    <div role="status" aria-label="Loading page" className="space-y-4 p-6">
      <Skeleton className="h-8 w-64" />
      <Skeleton className="h-32 w-full max-w-3xl" />
    </div>
  );
}

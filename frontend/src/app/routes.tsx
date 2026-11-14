import { lazy } from 'react';
import { Navigate, type RouteObject } from 'react-router-dom';
import { GuestRoute, ProtectedRoute } from '@/features/auth/components/ProtectedRoute';
import { LoginPage } from '@/features/auth/pages/LoginPage';
import { RegisterPage } from '@/features/auth/pages/RegisterPage';
import { SettingsPage } from '@/features/settings/pages/SettingsPage';
import { WorkspacePage } from '@/features/workspaces/pages/WorkspacePage';
import { WorkspacesPage } from '@/features/workspaces/pages/WorkspacesPage';
import { AppLayout } from '@/shared/layout/AppLayout';
import { NotFoundPage } from '@/shared/pages/NotFoundPage';

// The board screen pulls in drag & drop and SignalR, so it is loaded on demand.
const BoardPage = lazy(() =>
  import('@/features/boards/pages/BoardPage').then((module) => ({ default: module.BoardPage })),
);

export const routes: RouteObject[] = [
  {
    element: <GuestRoute />,
    children: [
      { path: '/login', element: <LoginPage /> },
      { path: '/register', element: <RegisterPage /> },
    ],
  },
  {
    element: <ProtectedRoute />,
    children: [
      {
        element: <AppLayout />,
        children: [
          { index: true, element: <Navigate to="/workspaces" replace /> },
          { path: '/workspaces', element: <WorkspacesPage /> },
          { path: '/workspaces/:workspaceId', element: <WorkspacePage /> },
          { path: '/boards/:boardId', element: <BoardPage /> },
          { path: '/settings', element: <SettingsPage /> },
          { path: '*', element: <NotFoundPage /> },
        ],
      },
    ],
  },
];

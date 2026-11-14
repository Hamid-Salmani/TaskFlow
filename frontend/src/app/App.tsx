import { QueryClientProvider } from '@tanstack/react-query';
import { createBrowserRouter, RouterProvider } from 'react-router-dom';
import { queryClient } from '@/shared/api/query-client';
import { useThemeEffect } from '@/shared/hooks/useThemeEffect';
import { Toaster } from '@/shared/ui';
import { routes } from './routes';

const router = createBrowserRouter(routes);

export function App() {
  useThemeEffect();

  return (
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
      <Toaster />
    </QueryClientProvider>
  );
}

import { screen } from '@testing-library/react';
import { Route, Routes, useLocation } from 'react-router-dom';
import { renderWithProviders, signIn, signOut } from '@/test/utils';
import { GuestRoute, ProtectedRoute, readRedirectTarget } from './ProtectedRoute';

function LoginProbe() {
  const location = useLocation();
  return <p>Login page, from: {readRedirectTarget(location.state) ?? 'nowhere'}</p>;
}

function renderRoutes(route: string) {
  return renderWithProviders(
    <Routes>
      <Route element={<GuestRoute />}>
        <Route path="/login" element={<LoginProbe />} />
      </Route>
      <Route element={<ProtectedRoute />}>
        <Route path="/boards/:id" element={<h1>Secret board</h1>} />
        <Route path="/workspaces" element={<h1>Workspaces</h1>} />
      </Route>
    </Routes>,
    { route },
  );
}

describe('route guards', () => {
  beforeEach(() => signOut());

  it('redirects anonymous users to /login and remembers where they wanted to go', () => {
    renderRoutes('/boards/42?status=open');

    expect(screen.getByText('Login page, from: /boards/42?status=open')).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Secret board' })).not.toBeInTheDocument();
  });

  it('renders protected content for signed-in users', () => {
    signIn();
    renderRoutes('/boards/42');

    expect(screen.getByRole('heading', { name: 'Secret board' })).toBeInTheDocument();
  });

  it('sends signed-in users away from guest-only pages', () => {
    signIn();
    renderRoutes('/login');

    expect(screen.getByRole('heading', { name: 'Workspaces' })).toBeInTheDocument();
  });
});

describe('readRedirectTarget', () => {
  it.each([
    [{ from: '/boards/1' }, '/boards/1'],
    [{ from: 'https://evil.example' }, null],
    [{ from: 42 }, null],
    [null, null],
    ['x', null],
  ])('reads %j as %j', (state, expected) => {
    expect(readRedirectTarget(state)).toBe(expected);
  });
});

import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { Route, Routes } from 'react-router-dom';
import { useSessionStore } from '@/shared/store/session-store';
import { server } from '@/test/server';
import { API, makeAuthResponse, renderWithProviders } from '@/test/utils';
import { LoginPage } from './LoginPage';

function renderLogin(route = '/login') {
  return renderWithProviders(
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/workspaces" element={<h1>Workspaces home</h1>} />
      <Route path="/boards/:id" element={<h1>Board screen</h1>} />
    </Routes>,
    { route },
  );
}

describe('LoginPage', () => {
  beforeEach(() => useSessionStore.getState().clear());

  it('validates required fields before calling the API', async () => {
    const user = userEvent.setup();
    renderLogin();

    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByText('Email is required')).toBeInTheDocument();
    expect(screen.getByText('Password is required')).toBeInTheDocument();
    expect(screen.getByLabelText('Email')).toHaveAttribute('aria-invalid', 'true');
  });

  it('rejects a malformed email', async () => {
    const user = userEvent.setup();
    renderLogin();

    await user.type(screen.getByLabelText('Email'), 'nope');
    await user.type(screen.getByLabelText('Password'), 'secret');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByText('Enter a valid email address')).toBeInTheDocument();
  });

  it('shows a form-level error for invalid credentials', async () => {
    const user = userEvent.setup();
    server.use(
      http.post(`${API}/auth/login`, () =>
        HttpResponse.json(
          { status: 401, title: 'Unauthorized', detail: 'bad', code: 'auth.invalid_credentials' },
          { status: 401 },
        ),
      ),
    );
    renderLogin();

    await user.type(screen.getByLabelText('Email'), 'ada@test.dev');
    await user.type(screen.getByLabelText('Password'), 'wrong');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Invalid email or password.');
    expect(useSessionStore.getState().session).toBeNull();
  });

  it('shows a network failure message', async () => {
    const user = userEvent.setup();
    server.use(http.post(`${API}/auth/login`, () => HttpResponse.error()));
    renderLogin();

    await user.type(screen.getByLabelText('Email'), 'ada@test.dev');
    await user.type(screen.getByLabelText('Password'), 'secret');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(/could not reach the server/i);
  });

  it('stores the session and navigates to the workspaces on success', async () => {
    const user = userEvent.setup();
    let body: unknown;
    server.use(
      http.post(`${API}/auth/login`, async ({ request }) => {
        body = await request.json();
        return HttpResponse.json(makeAuthResponse(), { status: 200 });
      }),
    );
    renderLogin();

    await user.type(screen.getByLabelText('Email'), 'ada@test.dev');
    await user.type(screen.getByLabelText('Password'), 'Password123');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByRole('heading', { name: 'Workspaces home' })).toBeInTheDocument();
    expect(body).toEqual({ email: 'ada@test.dev', password: 'Password123' });
    expect(useSessionStore.getState().session?.accessToken).toBe('access-token');
  });

  it('returns to the page the user originally asked for', async () => {
    const user = userEvent.setup();
    server.use(http.post(`${API}/auth/login`, () => HttpResponse.json(makeAuthResponse())));
    renderWithProviders(
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/boards/:id" element={<h1>Board screen</h1>} />
      </Routes>,
      { route: { pathname: '/login', state: { from: '/boards/42' } } as unknown as string },
    );

    await user.type(screen.getByLabelText('Email'), 'ada@test.dev');
    await user.type(screen.getByLabelText('Password'), 'Password123');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    await waitFor(() =>
      expect(screen.getByRole('heading', { name: 'Board screen' })).toBeInTheDocument(),
    );
  });

  it('links to the registration page', () => {
    renderLogin();

    expect(screen.getByRole('link', { name: 'Create an account' })).toHaveAttribute(
      'href',
      '/register',
    );
  });
});

import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { Route, Routes } from 'react-router-dom';
import type { WorkspaceDto } from '@/shared/api/types';
import { server } from '@/test/server';
import { API, renderWithProviders, signIn } from '@/test/utils';
import { WorkspacesPage } from './WorkspacesPage';

const acme: WorkspaceDto = {
  id: 'w1',
  name: 'Acme',
  description: 'Product team',
  myRole: 'Owner',
  memberCount: 3,
  boardCount: 1,
};

function renderPage() {
  return renderWithProviders(
    <Routes>
      <Route path="/workspaces" element={<WorkspacesPage />} />
      <Route path="/workspaces/:id" element={<h1>Workspace screen</h1>} />
    </Routes>,
    { route: '/workspaces' },
  );
}

describe('WorkspacesPage', () => {
  beforeEach(() => signIn());

  it('shows a loading skeleton, then the workspaces with role and counts', async () => {
    server.use(http.get(`${API}/workspaces`, () => HttpResponse.json([acme])));
    renderPage();

    expect(screen.getByLabelText('Loading workspaces')).toBeInTheDocument();

    const link = await screen.findByRole('link', { name: /Acme/ });
    expect(link).toHaveAttribute('href', '/workspaces/w1');
    expect(within(link).getByText('Owner')).toBeInTheDocument();
    expect(within(link).getByText('3 members')).toBeInTheDocument();
    expect(within(link).getByText('1 board')).toBeInTheDocument();
  });

  it('invites the user to create the first workspace when there are none', async () => {
    server.use(http.get(`${API}/workspaces`, () => HttpResponse.json([])));
    renderPage();

    expect(
      await screen.findByRole('heading', { name: 'Create your first workspace' }),
    ).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: 'New workspace' })).not.toHaveLength(0);
  });

  it('shows an error with a retry that refetches', async () => {
    const user = userEvent.setup();
    let calls = 0;
    server.use(
      http.get(`${API}/workspaces`, () => {
        calls += 1;
        return calls === 1
          ? HttpResponse.json({ status: 500, detail: 'Database is down' }, { status: 500 })
          : HttpResponse.json([acme]);
      }),
    );
    renderPage();

    expect(await screen.findByRole('alert')).toHaveTextContent('Database is down');
    await user.click(screen.getByRole('button', { name: 'Try again' }));

    expect(await screen.findByRole('link', { name: /Acme/ })).toBeInTheDocument();
  });

  it('creates a workspace and navigates to it', async () => {
    const user = userEvent.setup();
    let body: unknown;
    server.use(
      http.get(`${API}/workspaces`, () => HttpResponse.json([acme])),
      http.post(`${API}/workspaces`, async ({ request }) => {
        body = await request.json();
        return HttpResponse.json({ ...acme, id: 'w2', name: 'Design' }, { status: 201 });
      }),
    );
    renderPage();

    await user.click(await screen.findByRole('button', { name: 'New workspace' }));
    const dialog = screen.getByRole('dialog', { name: 'New workspace' });
    await user.type(within(dialog).getByLabelText('Name'), 'Design');
    await user.click(within(dialog).getByRole('button', { name: 'Create workspace' }));

    expect(await screen.findByRole('heading', { name: 'Workspace screen' })).toBeInTheDocument();
    expect(body).toEqual({ name: 'Design', description: null });
  });

  it('validates the name on the client and shows server errors in the dialog', async () => {
    const user = userEvent.setup();
    server.use(
      http.get(`${API}/workspaces`, () => HttpResponse.json([acme])),
      http.post(`${API}/workspaces`, () =>
        HttpResponse.json(
          { status: 400, code: 'validation.failed', errors: { name: ['Name is taken'] } },
          { status: 400 },
        ),
      ),
    );
    renderPage();

    await user.click(await screen.findByRole('button', { name: 'New workspace' }));
    const dialog = screen.getByRole('dialog', { name: 'New workspace' });
    await user.click(within(dialog).getByRole('button', { name: 'Create workspace' }));
    expect(await within(dialog).findByText('Name is required')).toBeInTheDocument();

    await user.type(within(dialog).getByLabelText('Name'), 'Acme');
    await user.click(within(dialog).getByRole('button', { name: 'Create workspace' }));

    expect(await within(dialog).findByText('Name is taken')).toBeInTheDocument();
    expect(screen.getByRole('dialog', { name: 'New workspace' })).toBeInTheDocument();
  });

  it('closes the dialog with Escape without creating anything', async () => {
    const user = userEvent.setup();
    server.use(http.get(`${API}/workspaces`, () => HttpResponse.json([acme])));
    renderPage();

    await user.click(await screen.findByRole('button', { name: 'New workspace' }));
    await user.keyboard('{Escape}');

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });
});

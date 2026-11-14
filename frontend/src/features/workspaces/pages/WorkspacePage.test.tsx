import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { Route, Routes } from 'react-router-dom';
import type {
  BoardSummaryDto,
  WorkspaceDto,
  WorkspaceMemberDto,
  WorkspaceRole,
} from '@/shared/api/types';
import { server } from '@/test/server';
import { API, renderWithProviders, signIn, testUser } from '@/test/utils';
import { WorkspacePage } from './WorkspacePage';

const board: BoardSummaryDto = {
  id: 'b1',
  workspaceId: 'w1',
  name: 'Roadmap',
  description: null,
  isArchived: false,
  createdAt: '2026-01-01T00:00:00Z',
};

function memberOf(userId: string, name: string, role: WorkspaceRole): WorkspaceMemberDto {
  return {
    userId,
    email: `${name.toLowerCase()}@test.dev`,
    displayName: name,
    handle: name.toLowerCase(),
    role,
    joinedAt: '2026-01-01T00:00:00Z',
  };
}

/** The signed-in test user (Ada) holds `myRole`; Olivia owns the workspace unless Ada does. */
function mockWorkspace(myRole: WorkspaceRole, extraMembers: WorkspaceMemberDto[] = []) {
  const workspace: WorkspaceDto = {
    id: 'w1',
    name: 'Acme',
    description: null,
    myRole,
    memberCount: 3,
    boardCount: 1,
  };
  const members = [
    myRole === 'Owner'
      ? memberOf(testUser.id, 'Ada', 'Owner')
      : memberOf('owner-id', 'Olivia', 'Owner'),
    ...(myRole === 'Owner' ? [] : [memberOf(testUser.id, 'Ada', myRole)]),
    ...extraMembers,
  ];
  server.use(
    http.get(`${API}/workspaces/w1`, () => HttpResponse.json(workspace)),
    http.get(`${API}/workspaces/w1/members`, () => HttpResponse.json(members)),
    http.get(`${API}/workspaces/w1/boards`, () => HttpResponse.json([board])),
  );
}

function renderPage() {
  return renderWithProviders(
    <Routes>
      <Route path="/workspaces/:workspaceId" element={<WorkspacePage />} />
      <Route path="/workspaces" element={<h1>Workspaces home</h1>} />
    </Routes>,
    { route: '/workspaces/w1' },
  );
}

describe('WorkspacePage role-based UI', () => {
  beforeEach(() => signIn());

  it('shows a plain member only what they may do', async () => {
    mockWorkspace('Member');
    renderPage();

    expect(await screen.findByRole('heading', { name: 'Acme' })).toBeInTheDocument();
    expect(await screen.findByRole('link', { name: /Roadmap/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'New board' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Edit' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Delete' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Actions for Roadmap/ })).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Invite by email')).not.toBeInTheDocument();
    expect(screen.queryByRole('combobox', { name: /Role of/ })).not.toBeInTheDocument();
    // Members can always leave.
    expect(await screen.findByRole('button', { name: 'Leave workspace' })).toBeInTheDocument();
  });

  it('gives admins editing, board management and invitations but not workspace deletion', async () => {
    mockWorkspace('Admin', [memberOf('m1', 'Mia', 'Member'), memberOf('a2', 'Ben', 'Admin')]);
    renderPage();

    expect(await screen.findByRole('button', { name: 'Edit' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Delete' })).not.toBeInTheDocument();
    expect(await screen.findByRole('button', { name: 'Actions for Roadmap' })).toBeInTheDocument();
    expect(screen.getByLabelText('Invite by email')).toBeInTheDocument();
    // Admins may change plain members, but not the owner or another admin.
    expect(screen.getByRole('combobox', { name: 'Role of Mia' })).toBeInTheDocument();
    expect(screen.queryByRole('combobox', { name: 'Role of Ben' })).not.toBeInTheDocument();
    expect(screen.queryByRole('combobox', { name: 'Role of Olivia' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Remove Mia' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Remove Ben' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Remove Olivia' })).not.toBeInTheDocument();
    // An admin can only hand out the member role.
    const roleOptions = within(screen.getByLabelText('Role')).getAllByRole('option');
    expect(roleOptions.map((option) => option.textContent)).toEqual(['Member']);
  });

  it('gives the owner everything, including granting admin and deleting the workspace', async () => {
    mockWorkspace('Owner', [memberOf('a2', 'Ben', 'Admin')]);
    renderPage();

    expect(await screen.findByRole('button', { name: 'Delete' })).toBeInTheDocument();
    expect(await screen.findByRole('combobox', { name: 'Role of Ben' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Leave workspace' })).not.toBeInTheDocument();
    const roleOptions = within(screen.getByLabelText('Role')).getAllByRole('option');
    expect(roleOptions.map((option) => option.textContent)).toEqual(['Member', 'Admin']);
  });

  it('shows a not-found message with a way back when the workspace is hidden from the user', async () => {
    server.use(
      http.get(`${API}/workspaces/w1`, () =>
        HttpResponse.json({ status: 404, code: 'resource.not_found' }, { status: 404 }),
      ),
    );
    renderPage();

    expect(await screen.findByRole('alert')).toHaveTextContent(/not a member/i);
    expect(screen.getByRole('link', { name: 'Back to workspaces' })).toHaveAttribute(
      'href',
      '/workspaces',
    );
  });

  it('invites a member and reports an unknown email on the field', async () => {
    const user = userEvent.setup();
    mockWorkspace('Owner');
    let body: unknown;
    server.use(
      http.post(`${API}/workspaces/w1/members`, async ({ request }) => {
        body = await request.json();
        return HttpResponse.json(
          {
            status: 404,
            code: 'user.not_found',
            detail: 'No registered user has that email address.',
          },
          { status: 404 },
        );
      }),
    );
    renderPage();

    await user.type(await screen.findByLabelText('Invite by email'), 'ghost@test.dev');
    await user.click(screen.getByRole('button', { name: 'Invite' }));

    expect(
      await screen.findByText('No registered user has that email address.'),
    ).toBeInTheDocument();
    expect(body).toEqual({ email: 'ghost@test.dev', role: 'Member' });
  });

  it('changes a role through the select', async () => {
    const user = userEvent.setup();
    mockWorkspace('Owner', [memberOf('m1', 'Mia', 'Member')]);
    let body: unknown;
    server.use(
      http.put(`${API}/workspaces/w1/members/m1`, async ({ request }) => {
        body = await request.json();
        return HttpResponse.json(memberOf('m1', 'Mia', 'Admin'));
      }),
    );
    renderPage();

    await user.selectOptions(await screen.findByRole('combobox', { name: 'Role of Mia' }), 'Admin');

    await screen.findByRole('combobox', { name: 'Role of Mia' });
    expect(body).toEqual({ role: 'Admin' });
  });

  it('asks for confirmation before removing a member', async () => {
    const user = userEvent.setup();
    mockWorkspace('Owner', [memberOf('m1', 'Mia', 'Member')]);
    let deleted = false;
    server.use(
      http.delete(`${API}/workspaces/w1/members/m1`, () => {
        deleted = true;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    renderPage();

    await user.click(await screen.findByRole('button', { name: 'Remove Mia' }));
    const dialog = screen.getByRole('dialog', { name: 'Remove member' });
    expect(deleted).toBe(false);
    await user.click(within(dialog).getByRole('button', { name: 'Remove' }));

    await screen.findByText('Member removed');
    expect(deleted).toBe(true);
  });

  it('leaves the workspace and returns to the list', async () => {
    const user = userEvent.setup();
    mockWorkspace('Member');
    server.use(
      http.delete(
        `${API}/workspaces/w1/members/${testUser.id}`,
        () => new HttpResponse(null, { status: 204 }),
      ),
    );
    renderPage();

    await user.click(await screen.findByRole('button', { name: 'Leave workspace' }));
    await user.click(
      within(screen.getByRole('dialog', { name: 'Leave workspace' })).getByRole('button', {
        name: 'Leave',
      }),
    );

    expect(await screen.findByRole('heading', { name: 'Workspaces home' })).toBeInTheDocument();
  });

  it('creates a board from the boards section', async () => {
    const user = userEvent.setup();
    mockWorkspace('Member');
    let body: unknown;
    server.use(
      http.post(`${API}/workspaces/w1/boards`, async ({ request }) => {
        body = await request.json();
        return HttpResponse.json({ ...board, id: 'b2', name: 'Sprint' }, { status: 201 });
      }),
    );
    renderPage();

    await user.click(await screen.findByRole('button', { name: 'New board' }));
    const dialog = screen.getByRole('dialog', { name: 'New board' });
    await user.type(within(dialog).getByLabelText('Name'), 'Sprint');
    await user.click(within(dialog).getByRole('button', { name: 'Create board' }));

    await screen.findByRole('heading', { name: 'Acme' });
    expect(body).toEqual({ name: 'Sprint', description: null });
  });
});

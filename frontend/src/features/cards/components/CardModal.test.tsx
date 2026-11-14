import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { Route, Routes, useLocation } from 'react-router-dom';
import { BoardPage } from '@/features/boards/pages/BoardPage';
import type {
  ActivityDto,
  BoardDetailDto,
  CardDto,
  CommentDto,
  LabelDto,
  WorkspaceMemberDto,
} from '@/shared/api/types';
import { server } from '@/test/server';
import { API, makeBoard, makeCard, renderWithProviders, signIn, testUser } from '@/test/utils';

const ME = testUser.id;
const OTHER = '22222222-2222-2222-2222-222222222222';

const bug: LabelDto = { id: 'l1', boardId: 'board-1', name: 'Bug', color: '#ef4444' };

function member(userId: string, displayName: string): WorkspaceMemberDto {
  return {
    userId,
    email: `${displayName}@t.dev`,
    displayName,
    handle: displayName.toLowerCase(),
    role: 'Member',
    joinedAt: '2026-01-01T00:00:00Z',
  };
}

function comment(
  id: string,
  authorId: string,
  authorDisplayName: string,
  body: string,
): CommentDto {
  return {
    id,
    cardId: 'c1',
    authorId,
    authorDisplayName,
    body,
    createdAt: '2026-01-01T00:00:00Z',
    editedAt: null,
  };
}

interface Options {
  board?: Partial<BoardDetailDto>;
  card?: Partial<CardDto>;
  comments?: CommentDto[];
  activity?: ActivityDto[];
}

function setup({ board = {}, card = {}, comments = [], activity = [] }: Options = {}) {
  const state = {
    card: makeCard({
      id: 'c1',
      title: 'Ship it',
      description: 'Do the thing',
      version: 3,
      ...card,
    }),
    comments,
    requests: [] as Array<{ method: string; url: string; body: unknown }>,
  };
  const detail = makeBoard({
    labels: [bug],
    members: [member(ME, 'Ada'), member(OTHER, 'Grace')],
    ...board,
  });

  async function record(request: Request) {
    const text = await request.clone().text();
    state.requests.push({
      method: request.method,
      url: new URL(request.url).pathname.replace('/api/v1', ''),
      body: text ? JSON.parse(text) : null,
    });
  }

  server.use(
    http.get(`${API}/boards/board-1`, () => HttpResponse.json(detail)),
    http.get(`${API}/boards/board-1/cards`, () =>
      HttpResponse.json({
        items: [state.card],
        page: 1,
        pageSize: 200,
        totalCount: 1,
        totalPages: 1,
      }),
    ),
    http.get(`${API}/cards/c1/comments`, () =>
      HttpResponse.json({
        items: state.comments,
        page: 1,
        pageSize: 100,
        totalCount: state.comments.length,
        totalPages: 1,
      }),
    ),
    http.get(`${API}/cards/c1/activity`, () =>
      HttpResponse.json({
        items: activity,
        page: 1,
        pageSize: 50,
        totalCount: activity.length,
        totalPages: 1,
      }),
    ),
    http.put(`${API}/cards/c1`, async ({ request }) => {
      await record(request);
      const body = (await request.clone().json()) as Partial<CardDto>;
      state.card = { ...state.card, ...body, version: state.card.version + 1 };
      return HttpResponse.json(state.card);
    }),
    http.put(`${API}/cards/c1/labels`, async ({ request }) => {
      await record(request);
      const body = (await request.clone().json()) as { labelIds: string[] };
      state.card = { ...state.card, labelIds: body.labelIds, version: state.card.version + 1 };
      return HttpResponse.json(state.card);
    }),
    http.put(`${API}/cards/c1/assignees`, async ({ request }) => {
      await record(request);
      const body = (await request.clone().json()) as { userIds: string[] };
      state.card = { ...state.card, assigneeIds: body.userIds, version: state.card.version + 1 };
      return HttpResponse.json(state.card);
    }),
    http.delete(`${API}/cards/c1`, async ({ request }) => {
      await record(request);
      return new HttpResponse(null, { status: 204 });
    }),
    http.post(`${API}/cards/c1/comments`, async ({ request }) => {
      await record(request);
      const body = (await request.clone().json()) as { body: string };
      const created = comment('new', ME, 'Ada Lovelace', body.body);
      state.comments = [...state.comments, created];
      return HttpResponse.json(created, { status: 201 });
    }),
    http.put(`${API}/comments/:id`, async ({ request, params }) => {
      await record(request);
      const body = (await request.clone().json()) as { body: string };
      state.comments = state.comments.map((c) =>
        c.id === params['id'] ? { ...c, body: body.body, editedAt: '2026-01-02T00:00:00Z' } : c,
      );
      return HttpResponse.json(state.comments.find((c) => c.id === params['id']));
    }),
    http.delete(`${API}/comments/:id`, async ({ request, params }) => {
      await record(request);
      state.comments = state.comments.filter((c) => c.id !== params['id']);
      return new HttpResponse(null, { status: 204 });
    }),
  );

  signIn();
  const view = renderWithProviders(
    <>
      <output aria-label="location">{<Where />}</output>
      <Routes>
        <Route path="/boards/:boardId" element={<BoardPage />} />
      </Routes>
    </>,
    { route: '/boards/board-1?card=c1' },
  );
  return { state, ...view };
}

function Where() {
  return useLocation().search;
}

async function openDialog() {
  return screen.findByRole('dialog', { name: 'Ship it' });
}

describe('CardModal', () => {
  it('opens from the URL and shows the card details, comments and activity', async () => {
    setup({
      comments: [comment('k1', OTHER, 'Grace', 'Ping @ada please')],
      activity: [
        {
          id: 'a1',
          boardId: 'board-1',
          cardId: 'c1',
          userId: OTHER,
          userDisplayName: 'Grace',
          type: 'CardCreated',
          message: 'Created card "Ship it"',
          createdAt: '2026-01-01T00:00:00Z',
        },
      ],
    });
    const dialog = await openDialog();

    expect(within(dialog).getByLabelText('Title')).toHaveValue('Ship it');
    expect(within(dialog).getByLabelText('Description')).toHaveValue('Do the thing');
    expect(await within(dialog).findByText('@ada')).toHaveClass('text-primary');
    expect(await within(dialog).findByText(/Created card "Ship it"/)).toBeInTheDocument();
  });

  it('saves edits with the version the user saw', async () => {
    const user = userEvent.setup();
    const { state } = setup();
    const dialog = await openDialog();

    const title = within(dialog).getByLabelText('Title');
    await user.clear(title);
    await user.type(title, 'Ship it today');
    await user.type(within(dialog).getByLabelText('Due date'), '2026-05-04');
    await user.click(within(dialog).getByRole('button', { name: 'Save changes' }));

    await waitFor(() => expect(state.requests[0]).toBeDefined());
    expect(state.requests[0]).toMatchObject({
      method: 'PUT',
      url: '/cards/c1',
      body: {
        expectedVersion: 3,
        title: 'Ship it today',
        description: 'Do the thing',
        dueDate: '2026-05-04T00:00:00Z',
      },
    });
    expect(await screen.findByText('Card saved')).toBeInTheDocument();
  });

  it('validates the title before calling the API', async () => {
    const user = userEvent.setup();
    const { state } = setup();
    const dialog = await openDialog();

    await user.clear(within(dialog).getByLabelText('Title'));
    await user.click(within(dialog).getByRole('button', { name: 'Save changes' }));

    expect(await within(dialog).findByText('Title is required')).toBeInTheDocument();
    expect(state.requests).toHaveLength(0);
  });

  it('tells the user when somebody else changed the card first (409)', async () => {
    const user = userEvent.setup();
    setup();
    server.use(
      http.put(`${API}/cards/c1`, () =>
        HttpResponse.json(
          { status: 409, code: 'card.version_conflict', detail: 'changed' },
          { status: 409 },
        ),
      ),
    );
    const dialog = await openDialog();

    await user.type(within(dialog).getByLabelText('Title'), '!');
    await user.click(within(dialog).getByRole('button', { name: 'Save changes' }));

    expect(await screen.findByText('Card was changed')).toBeInTheDocument();
  });

  it('toggles a label and an assignee, each against the current version', async () => {
    const user = userEvent.setup();
    const { state } = setup();
    const dialog = await openDialog();

    await user.click(within(dialog).getByRole('button', { name: 'Bug' }));
    await waitFor(() =>
      expect(within(dialog).getByRole('button', { name: 'Bug' })).toHaveAttribute(
        'aria-pressed',
        'true',
      ),
    );
    await user.click(within(dialog).getByRole('button', { name: /Grace/ }));

    await waitFor(() => expect(state.requests).toHaveLength(2));
    expect(state.requests[0]).toMatchObject({
      url: '/cards/c1/labels',
      body: { expectedVersion: 3, labelIds: ['l1'] },
    });
    expect(state.requests[1]).toMatchObject({
      url: '/cards/c1/assignees',
      body: { expectedVersion: 4, userIds: [OTHER] },
    });
  });

  it('adds a comment', async () => {
    const user = userEvent.setup();
    const { state } = setup();
    const dialog = await openDialog();

    await user.type(within(dialog).getByLabelText('Write a comment'), 'Looks good{Enter}');

    expect(await within(dialog).findByText('Looks good')).toBeInTheDocument();
    expect(state.requests[0]).toMatchObject({
      url: '/cards/c1/comments',
      body: { body: 'Looks good' },
    });
  });

  it("lets authors edit and delete their own comments but not other people's", async () => {
    const user = userEvent.setup();
    const { state } = setup({
      board: { myRole: 'Member' },
      comments: [
        comment('mine', ME, 'Ada Lovelace', 'my words'),
        comment('theirs', OTHER, 'Grace', 'their words'),
      ],
    });
    const dialog = await openDialog();
    await within(dialog).findByText('my words');

    expect(
      within(dialog).getByRole('button', { name: 'Edit comment by Ada Lovelace' }),
    ).toBeInTheDocument();
    expect(
      within(dialog).getByRole('button', { name: 'Delete comment by Ada Lovelace' }),
    ).toBeInTheDocument();
    expect(
      within(dialog).queryByRole('button', { name: 'Edit comment by Grace' }),
    ).not.toBeInTheDocument();
    expect(
      within(dialog).queryByRole('button', { name: 'Delete comment by Grace' }),
    ).not.toBeInTheDocument();

    await user.click(within(dialog).getByRole('button', { name: 'Edit comment by Ada Lovelace' }));
    const editor = within(dialog).getByLabelText('Edit comment');
    await user.clear(editor);
    await user.type(editor, 'fixed words{Enter}');
    expect(await within(dialog).findByText('fixed words')).toBeInTheDocument();
    expect(await within(dialog).findByText(/\(edited\)/)).toBeInTheDocument();
    expect(state.requests[0]).toMatchObject({
      method: 'PUT',
      url: '/comments/mine',
      body: { body: 'fixed words' },
    });
  });

  it('lets admins delete any comment, after confirming', async () => {
    const user = userEvent.setup();
    const { state } = setup({
      board: { myRole: 'Admin' },
      comments: [comment('theirs', OTHER, 'Grace', 'their words')],
    });
    const dialog = await openDialog();
    await within(dialog).findByText('their words');

    expect(
      within(dialog).queryByRole('button', { name: 'Edit comment by Grace' }),
    ).not.toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: 'Delete comment by Grace' }));
    await user.click(await screen.findByRole('button', { name: 'Delete comment' }));

    await waitFor(() =>
      expect(state.requests[0]).toMatchObject({ method: 'DELETE', url: '/comments/theirs' }),
    );
    await waitFor(() => expect(within(dialog).queryByText('their words')).not.toBeInTheDocument());
  });

  it('deletes the card after confirmation and closes the dialog', async () => {
    const user = userEvent.setup();
    const { state } = setup();
    const dialog = await openDialog();

    await user.click(within(dialog).getByRole('button', { name: 'Delete card' }));
    const confirm = await screen.findByRole('dialog', { name: 'Delete card' });
    await user.click(within(confirm).getByRole('button', { name: 'Delete card' }));

    await waitFor(() =>
      expect(state.requests[0]).toMatchObject({ method: 'DELETE', url: '/cards/c1' }),
    );
    await waitFor(() =>
      expect(screen.queryByRole('dialog', { name: 'Ship it' })).not.toBeInTheDocument(),
    );
  });

  it('is read-only on an archived board', async () => {
    setup({ board: { isArchived: true }, comments: [comment('mine', ME, 'Ada Lovelace', 'old')] });
    const dialog = await openDialog();

    expect(within(dialog).getByLabelText('Title')).toBeDisabled();
    expect(within(dialog).getByLabelText('Description')).toBeDisabled();
    expect(within(dialog).queryByRole('button', { name: 'Save changes' })).not.toBeInTheDocument();
    expect(within(dialog).queryByLabelText('Write a comment')).not.toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'Bug' })).toBeDisabled();
    await within(dialog).findByText('old');
    expect(within(dialog).queryByRole('button', { name: /Edit comment/ })).not.toBeInTheDocument();
  });

  it('closes with Escape, removes the card from the URL and returns focus to the board', async () => {
    const user = userEvent.setup();
    setup();
    await openDialog();

    await user.keyboard('{Escape}');

    await waitFor(() =>
      expect(screen.queryByRole('dialog', { name: 'Ship it' })).not.toBeInTheDocument(),
    );
    expect(screen.getByLabelText('location')).not.toHaveTextContent('card=');
  });
});

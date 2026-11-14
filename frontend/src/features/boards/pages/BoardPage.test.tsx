import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { Route, Routes, useLocation } from 'react-router-dom';
import type { BoardDetailDto, CardDto, WorkspaceMemberDto } from '@/shared/api/types';
import { server } from '@/test/server';
import { API, makeBoard, makeCard, renderWithProviders, signIn } from '@/test/utils';
import { BoardPage } from './BoardPage';

const ada: WorkspaceMemberDto = {
  userId: 'u1',
  email: 'ada@test.dev',
  displayName: 'Ada Lovelace',
  handle: 'ada',
  role: 'Member',
  joinedAt: '2026-01-01T00:00:00Z',
};

function mockBoard(board: BoardDetailDto, cards: CardDto[]) {
  server.use(
    http.get(`${API}/boards/${board.id}`, () => HttpResponse.json(board)),
    http.get(`${API}/boards/${board.id}/cards`, () =>
      HttpResponse.json({
        items: cards,
        page: 1,
        pageSize: 200,
        totalCount: cards.length,
        totalPages: 1,
      }),
    ),
  );
}

function LocationProbe() {
  return <output aria-label="location">{useLocation().search}</output>;
}

function renderBoard() {
  signIn();
  return renderWithProviders(
    <>
      <LocationProbe />
      <Routes>
        <Route path="/boards/:boardId" element={<BoardPage />} />
      </Routes>
    </>,
    { route: '/boards/board-1' },
  );
}

describe('BoardPage', () => {
  it('shows a skeleton, then the columns with their cards in position order', async () => {
    mockBoard(makeBoard({ members: [ada] }), [
      makeCard({ id: 'c2', title: 'Second', position: 2048, assigneeIds: ['u1'] }),
      makeCard({ id: 'c1', title: 'First', position: 1024, commentCount: 3 }),
      makeCard({ id: 'c3', title: 'Elsewhere', columnId: 'col-2' }),
    ]);
    renderBoard();

    expect(screen.getByLabelText('Loading board')).toBeInTheDocument();

    const todo = await screen.findByRole('region', { name: 'To do, 2 cards' });
    const titles = within(todo)
      .getAllByRole('button', { name: /Press Enter to open/ })
      .map((card) => card.textContent);
    expect(titles[0]).toContain('First');
    expect(titles[1]).toContain('Second');
    expect(screen.getByRole('region', { name: 'Doing, 1 card' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Roadmap' })).toBeInTheDocument();
  });

  it('adds a card to a column and keeps the composer open for the next one', async () => {
    const user = userEvent.setup();
    let body: unknown;
    mockBoard(makeBoard(), []);
    server.use(
      http.post(`${API}/columns/col-1/cards`, async ({ request }) => {
        body = await request.json();
        return HttpResponse.json(makeCard({ id: 'new', title: 'Write docs' }), { status: 201 });
      }),
    );
    renderBoard();

    const todo = await screen.findByRole('region', { name: 'To do, 0 cards' });
    await user.click(within(todo).getByRole('button', { name: 'Add a card' }));
    await user.type(within(todo).getByLabelText('Card title'), 'Write docs{Enter}');

    expect(await screen.findByRole('region', { name: 'To do, 1 card' })).toBeInTheDocument();
    expect(body).toMatchObject({ title: 'Write docs' });
    expect(within(todo).getByLabelText('Card title')).toHaveValue('');
  });

  it('validates an empty card title without calling the API', async () => {
    const user = userEvent.setup();
    mockBoard(makeBoard(), []);
    renderBoard();

    const todo = await screen.findByRole('region', { name: 'To do, 0 cards' });
    await user.click(within(todo).getByRole('button', { name: 'Add a card' }));
    await user.click(within(todo).getByRole('button', { name: 'Add card' }));

    expect(await within(todo).findByRole('alert')).toHaveTextContent('Enter a title');
  });

  it('adds a column', async () => {
    const user = userEvent.setup();
    mockBoard(makeBoard(), []);
    server.use(
      http.post(`${API}/boards/board-1/columns`, () =>
        HttpResponse.json(
          { id: 'col-3', boardId: 'board-1', title: 'Review', position: 3072 },
          { status: 201 },
        ),
      ),
    );
    renderBoard();

    await user.click(await screen.findByRole('button', { name: 'Add a column' }));
    await user.type(screen.getByLabelText('Column title'), 'Review{Enter}');

    expect(await screen.findByRole('region', { name: 'Review, 0 cards' })).toBeInTheDocument();
  });

  it('only offers "Delete column" to admins', async () => {
    const user = userEvent.setup();
    mockBoard(makeBoard({ myRole: 'Member' }), []);
    const { unmount } = renderBoard();

    await user.click(await screen.findByRole('button', { name: 'Actions for column To do' }));
    expect(screen.getByRole('menuitem', { name: 'Rename' })).toBeInTheDocument();
    expect(screen.queryByRole('menuitem', { name: 'Delete column' })).not.toBeInTheDocument();
    unmount();

    mockBoard(makeBoard({ myRole: 'Admin' }), []);
    renderBoard();
    await user.click(await screen.findByRole('button', { name: 'Actions for column To do' }));
    expect(screen.getByRole('menuitem', { name: 'Delete column' })).toBeInTheDocument();
  });

  it('is read-only when the board is archived', async () => {
    mockBoard(makeBoard({ isArchived: true }), [makeCard({ title: 'Frozen' })]);
    renderBoard();

    expect(await screen.findByText(/Archived/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Add a card' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Add a column' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Move column/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Actions for column/ })).not.toBeInTheDocument();
  });

  it('shows a not-found message when the board is not accessible', async () => {
    server.use(
      http.get(`${API}/boards/board-1`, () =>
        HttpResponse.json({ status: 404, code: 'resource.not_found' }, { status: 404 }),
      ),
      http.get(`${API}/boards/board-1/cards`, () =>
        HttpResponse.json({ status: 404, code: 'resource.not_found' }, { status: 404 }),
      ),
    );
    renderBoard();

    expect(await screen.findByText(/does not exist or you are not a member/i)).toBeInTheDocument();
  });

  it('opens a card by putting its id in the URL', async () => {
    const user = userEvent.setup();
    mockBoard(makeBoard(), [makeCard({ id: 'c1', title: 'Open me' })]);
    renderBoard();

    await user.click(await screen.findByRole('button', { name: /Open me/ }));

    expect(screen.getByLabelText('location')).toHaveTextContent('?card=c1');
  });
});

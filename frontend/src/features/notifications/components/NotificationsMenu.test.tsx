import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { Route, Routes, useLocation } from 'react-router-dom';
import type { NotificationDto } from '@/shared/api/types';
import { server } from '@/test/server';
import { API, renderWithProviders, signIn } from '@/test/utils';
import { NotificationsMenu } from './NotificationsMenu';

function notification(overrides: Partial<NotificationDto> = {}): NotificationDto {
  return {
    id: 'n1',
    type: 'Mentioned',
    message: 'Grace mentioned you on "Ship it"',
    boardId: 'b1',
    cardId: 'c1',
    isRead: false,
    createdAt: '2026-01-01T00:00:00Z',
    ...overrides,
  };
}

function mockInbox(items: NotificationDto[]) {
  const state = { items, readIds: [] as string[], readAll: 0 };
  server.use(
    http.get(`${API}/notifications/unread-count`, () =>
      HttpResponse.json({ count: state.items.filter((n) => !n.isRead).length }),
    ),
    http.get(`${API}/notifications`, () =>
      HttpResponse.json({
        items: state.items,
        page: 1,
        pageSize: 20,
        totalCount: state.items.length,
        totalPages: 1,
      }),
    ),
    http.post(`${API}/notifications/read-all`, () => {
      state.readAll += 1;
      state.items = state.items.map((n) => ({ ...n, isRead: true }));
      return new HttpResponse(null, { status: 204 });
    }),
    http.post(`${API}/notifications/:id/read`, ({ params }) => {
      state.readIds.push(String(params['id']));
      state.items = state.items.map((n) => (n.id === params['id'] ? { ...n, isRead: true } : n));
      return new HttpResponse(null, { status: 204 });
    }),
  );
  return state;
}

function Where() {
  const location = useLocation();
  return <output aria-label="location">{location.pathname + location.search}</output>;
}

function renderMenu() {
  signIn();
  return renderWithProviders(
    <>
      <NotificationsMenu />
      <Routes>
        <Route path="*" element={<Where />} />
      </Routes>
    </>,
  );
}

describe('NotificationsMenu', () => {
  it('shows the unread count in the badge and the accessible name', async () => {
    mockInbox([
      notification(),
      notification({ id: 'n2' }),
      notification({ id: 'n3', isRead: true }),
    ]);
    renderMenu();

    expect(
      await screen.findByRole('button', { name: 'Notifications, 2 unread' }),
    ).toBeInTheDocument();
  });

  it('has no badge when everything is read', async () => {
    mockInbox([notification({ isRead: true })]);
    renderMenu();

    expect(await screen.findByRole('button', { name: 'Notifications' })).toBeInTheDocument();
  });

  it('lists notifications; selecting one marks it read and opens its card', async () => {
    const user = userEvent.setup();
    const inbox = mockInbox([notification()]);
    renderMenu();

    await user.click(await screen.findByRole('button', { name: /Notifications/ }));
    await user.click(await screen.findByRole('menuitem', { name: /Grace mentioned you/ }));

    await waitFor(() => expect(inbox.readIds).toEqual(['n1']));
    expect(screen.getByLabelText('location')).toHaveTextContent('/boards/b1?card=c1');
  });

  it('marks everything read', async () => {
    const user = userEvent.setup();
    const inbox = mockInbox([notification(), notification({ id: 'n2' })]);
    renderMenu();

    await user.click(await screen.findByRole('button', { name: /Notifications/ }));
    await user.click(await screen.findByRole('button', { name: 'Mark all read' }));

    await waitFor(() => expect(inbox.readAll).toBe(1));
    expect(await screen.findByRole('button', { name: 'Notifications' })).toBeInTheDocument();
  });

  it('says so when there is nothing to show', async () => {
    const user = userEvent.setup();
    mockInbox([]);
    renderMenu();

    await user.click(await screen.findByRole('button', { name: /Notifications/ }));

    expect(await screen.findByText('You are all caught up.')).toBeInTheDocument();
    expect(within(screen.getByRole('menu')).queryAllByRole('menuitem')).toHaveLength(0);
  });
});

import { QueryClientProvider } from '@tanstack/react-query';
import { act, renderHook, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { EMPTY_FILTERS } from '@/features/cards/filters';
import { boardKeys } from '@/features/workspaces/query-keys';
import type { CardDto } from '@/shared/api/types';
import { createTestQueryClient, makeCard, signIn, testUser } from '@/test/utils';
import { useBoardRealtime, type BoardConnection } from './useBoardRealtime';

/** In-memory stand-in for the SignalR connection that lets a test push server events and connection changes. */
class FakeConnection implements BoardConnection {
  handlers = new Map<string, (payload: unknown) => void>();
  reconnecting: Array<() => void> = [];
  reconnected: Array<() => void> = [];
  closed: Array<() => void> = [];
  invocations: unknown[][] = [];
  startResults: Array<Error | undefined> = [];
  stopped = false;

  on(event: string, handler: (payload: unknown) => void) {
    this.handlers.set(event, handler);
  }
  start() {
    const failure = this.startResults.shift();
    return failure ? Promise.reject(failure) : Promise.resolve();
  }
  stop() {
    this.stopped = true;
    return Promise.resolve();
  }
  invoke(method: string, ...args: unknown[]) {
    this.invocations.push([method, ...args]);
    return Promise.resolve(undefined);
  }
  onreconnecting(handler: () => void) {
    this.reconnecting.push(handler);
  }
  onreconnected(handler: () => void) {
    this.reconnected.push(handler);
  }
  onclose(handler: () => void) {
    this.closed.push(handler);
  }
  emit(event: string, payload: unknown) {
    this.handlers.get(event)?.(payload);
  }
}

function setup(connection: FakeConnection) {
  signIn();
  const queryClient = createTestQueryClient();
  const factory = () => connection;
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
  const hook = renderHook(() => useBoardRealtime('board-1', factory), { wrapper });
  return { queryClient, ...hook };
}

const cardsKey = boardKeys.cards('board-1', EMPTY_FILTERS);

describe('useBoardRealtime', () => {
  it('connects, joins the board group and reports connected', async () => {
    const connection = new FakeConnection();
    const { result } = setup(connection);

    expect(result.current).toBe('connecting');
    await waitFor(() => expect(result.current).toBe('connected'));
    expect(connection.invocations).toEqual([['JoinBoard', 'board-1']]);
  });

  it("applies a teammate's move to the Query cache but ignores the user's own events", async () => {
    const connection = new FakeConnection();
    const { result, queryClient } = setup(connection);
    queryClient.setQueryData<CardDto[]>(cardsKey, [
      makeCard({ id: 'a', columnId: 'col-1', version: 1 }),
    ]);
    await waitFor(() => expect(result.current).toBe('connected'));

    const move = (actorUserId: string, version: number) => ({
      boardId: 'board-1',
      actorUserId,
      cardId: 'a',
      fromColumnId: 'col-1',
      toColumnId: 'col-2',
      position: 77,
      version,
      rebalanced: false,
    });

    act(() => connection.emit('CardMoved', move(testUser.id, 2)));
    expect(queryClient.getQueryData<CardDto[]>(cardsKey)?.[0]?.columnId).toBe('col-1');

    act(() => connection.emit('CardMoved', move('someone-else', 2)));
    expect(queryClient.getQueryData<CardDto[]>(cardsKey)?.[0]).toMatchObject({
      columnId: 'col-2',
      position: 77,
      version: 2,
    });
  });

  it('drops malformed events instead of corrupting the cache', async () => {
    const connection = new FakeConnection();
    const { result, queryClient } = setup(connection);
    queryClient.setQueryData<CardDto[]>(cardsKey, [makeCard({ id: 'a' })]);
    await waitFor(() => expect(result.current).toBe('connected'));

    act(() => connection.emit('CardDeleted', { boardId: 'board-1' }));

    expect(queryClient.getQueryData<CardDto[]>(cardsKey)).toHaveLength(1);
  });

  it('shows reconnecting, then rejoins and refetches the board once the connection is back', async () => {
    const connection = new FakeConnection();
    const { result, queryClient } = setup(connection);
    await waitFor(() => expect(result.current).toBe('connected'));
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries');

    act(() => connection.reconnecting.forEach((handler) => handler()));
    expect(result.current).toBe('reconnecting');

    act(() => connection.reconnected.forEach((handler) => handler()));
    await waitFor(() => expect(result.current).toBe('connected'));

    expect(connection.invocations.filter(([method]) => method === 'JoinBoard')).toHaveLength(2);
    expect(invalidate).toHaveBeenCalledWith({ queryKey: boardKeys.detail('board-1') });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: boardKeys.cardsOf('board-1') });
  });

  it('reports disconnected when the connection closes for good', async () => {
    const connection = new FakeConnection();
    const { result } = setup(connection);
    await waitFor(() => expect(result.current).toBe('connected'));

    act(() => connection.closed.forEach((handler) => handler()));

    expect(result.current).toBe('disconnected');
  });

  it('reports disconnected when the first connection attempt fails', async () => {
    const connection = new FakeConnection();
    connection.startResults = [new Error('offline')];
    const { result } = setup(connection);

    await waitFor(() => expect(result.current).toBe('disconnected'));
  });

  it('stops the connection on unmount', async () => {
    const connection = new FakeConnection();
    const { result, unmount } = setup(connection);
    await waitFor(() => expect(result.current).toBe('connected'));

    unmount();

    expect(connection.stopped).toBe(true);
  });
});

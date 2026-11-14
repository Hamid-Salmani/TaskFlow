import { QueryClientProvider } from '@tanstack/react-query';
import { act, renderHook, waitFor } from '@testing-library/react';
import { delay, http, HttpResponse } from 'msw';
import type { ReactNode } from 'react';
import { boardKeys } from '@/features/workspaces/query-keys';
import type { CardDto } from '@/shared/api/types';
import { toast } from '@/shared/ui';
import { useToastStore } from '@/shared/ui/toast-store';
import { server } from '@/test/server';
import { API, createTestQueryClient, makeCard } from '@/test/utils';
import { EMPTY_FILTERS } from '../filters';
import { useMoveCard } from './useMoveCard';

const BOARD = 'board-1';
const key = boardKeys.cards(BOARD, EMPTY_FILTERS);

function setup(cards: CardDto[]) {
  const queryClient = createTestQueryClient();
  queryClient.setQueryData(key, cards);
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
  const hook = renderHook(() => useMoveCard(BOARD), { wrapper });
  const cached = () => queryClient.getQueryData<CardDto[]>(key) ?? [];
  const card = (id: string) => cached().find((item) => item.id === id);
  return { queryClient, hook, cached, card };
}

const a = makeCard({ id: 'a', columnId: 'col-1', position: 1000, version: 1 });
const b = makeCard({ id: 'b', columnId: 'col-1', position: 2000, version: 1 });
const c = makeCard({ id: 'c', columnId: 'col-2', position: 1000, version: 4 });

describe('useMoveCard', () => {
  beforeEach(() => {
    useToastStore.setState({ toasts: [] });
  });

  it('updates the cache immediately, before the server has answered', async () => {
    let release: () => void = () => undefined;
    const gate = new Promise<void>((resolve) => {
      release = resolve;
    });
    server.use(
      http.put(`${API}/cards/a/move`, async () => {
        await gate;
        return HttpResponse.json({
          id: 'a',
          columnId: 'col-2',
          position: 500,
          version: 2,
          rebalanced: false,
        });
      }),
    );
    const { hook, card } = setup([a, b, c]);

    act(() =>
      hook.result.current.mutate({
        cardId: 'a',
        targetColumnId: 'col-2',
        afterId: null,
        beforeId: 'c',
      }),
    );

    await waitFor(() =>
      expect(card('a')).toMatchObject({ columnId: 'col-2', position: 1000 - 1024, version: 1 }),
    );
    expect(hook.result.current.isPending).toBe(true);

    release();
    await waitFor(() => expect(hook.result.current.isSuccess).toBe(true));
  });

  it('replaces the prediction with the position and version the server returns', async () => {
    server.use(
      http.put(`${API}/cards/a/move`, () =>
        HttpResponse.json({
          id: 'a',
          columnId: 'col-2',
          position: 4242,
          version: 9,
          rebalanced: false,
        }),
      ),
    );
    const { hook, card } = setup([a, b, c]);

    await act(() =>
      hook.result.current.mutateAsync({
        cardId: 'a',
        targetColumnId: 'col-2',
        afterId: 'c',
        beforeId: null,
      }),
    );

    expect(card('a')).toMatchObject({ columnId: 'col-2', position: 4242, version: 9 });
  });

  it('sends the neighbour ids and the card version', async () => {
    let body: unknown;
    server.use(
      http.put(`${API}/cards/c/move`, async ({ request }) => {
        body = await request.json();
        return HttpResponse.json({
          id: 'c',
          columnId: 'col-1',
          position: 1500,
          version: 5,
          rebalanced: false,
        });
      }),
    );
    const { hook } = setup([a, b, c]);

    await act(() =>
      hook.result.current.mutateAsync({
        cardId: 'c',
        targetColumnId: 'col-1',
        afterId: 'a',
        beforeId: 'b',
      }),
    );

    expect(body).toEqual({
      expectedVersion: 4,
      targetColumnId: 'col-1',
      afterId: 'a',
      beforeId: 'b',
    });
  });

  it('rolls back and shows an error toast when the request fails', async () => {
    server.use(
      http.put(`${API}/cards/a/move`, () =>
        HttpResponse.json({ status: 500, detail: 'Boom' }, { status: 500 }),
      ),
    );
    const { hook, cached } = setup([a, b, c]);

    await act(async () => {
      await hook.result.current
        .mutateAsync({ cardId: 'a', targetColumnId: 'col-2', afterId: null, beforeId: null })
        .catch(() => undefined);
    });

    expect(cached()).toEqual([a, b, c]);
    expect(useToastStore.getState().toasts.map((toastItem) => toastItem.title)).toContain(
      'Could not move the card',
    );
  });

  it('on a 409 conflict it rolls back, explains what happened and refetches the board', async () => {
    server.use(
      http.put(`${API}/cards/a/move`, () =>
        HttpResponse.json(
          { status: 409, code: 'card.version_conflict', detail: 'stale' },
          { status: 409 },
        ),
      ),
    );
    const { hook, cached, queryClient } = setup([a, b, c]);

    await act(async () => {
      await hook.result.current
        .mutateAsync({ cardId: 'a', targetColumnId: 'col-2', afterId: null, beforeId: null })
        .catch(() => undefined);
    });

    expect(cached()).toEqual([a, b, c]);
    expect(queryClient.getQueryState(key)?.isInvalidated).toBe(true);
    const titles = useToastStore.getState().toasts.map((toastItem) => toastItem.title);
    expect(titles).toContain('Card was changed');
    expect(titles).not.toContain('Could not move the card');
  });

  it('refetches the board when the server rebalanced the column', async () => {
    server.use(
      http.put(`${API}/cards/a/move`, () =>
        HttpResponse.json({
          id: 'a',
          columnId: 'col-1',
          position: 1536,
          version: 2,
          rebalanced: true,
        }),
      ),
    );
    const { hook, queryClient } = setup([a, b, c]);

    await act(() =>
      hook.result.current.mutateAsync({
        cardId: 'a',
        targetColumnId: 'col-1',
        afterId: 'b',
        beforeId: null,
      }),
    );

    expect(queryClient.getQueryState(key)?.isInvalidated).toBe(true);
  });

  it('updates every cached filter variant of the board', async () => {
    server.use(
      http.put(`${API}/cards/a/move`, () =>
        HttpResponse.json({
          id: 'a',
          columnId: 'col-2',
          position: 3000,
          version: 2,
          rebalanced: false,
        }),
      ),
    );
    const { hook, queryClient } = setup([a, b, c]);
    const filteredKey = boardKeys.cards(BOARD, { ...EMPTY_FILTERS, q: 'x' });
    queryClient.setQueryData(filteredKey, [a]);

    await act(() =>
      hook.result.current.mutateAsync({
        cardId: 'a',
        targetColumnId: 'col-2',
        afterId: null,
        beforeId: null,
      }),
    );

    expect(queryClient.getQueryData<CardDto[]>(filteredKey)?.[0]).toMatchObject({
      columnId: 'col-2',
      version: 2,
    });
  });

  it('runs two moves of the same board one after the other, so the second one carries the new version', async () => {
    const bodies: Array<{ id: string; expectedVersion: number }> = [];
    server.use(
      http.put(`${API}/cards/:id/move`, async ({ request, params }) => {
        const body = (await request.json()) as { expectedVersion: number };
        bodies.push({ id: String(params.id), expectedVersion: body.expectedVersion });
        await delay(30);
        return HttpResponse.json({
          id: params.id,
          columnId: 'col-2',
          position: bodies.length * 100,
          version: body.expectedVersion + 1,
          rebalanced: false,
        });
      }),
    );
    const { hook } = setup([a, b, c]);

    await act(async () => {
      const first = hook.result.current.mutateAsync({
        cardId: 'a',
        targetColumnId: 'col-2',
        afterId: null,
        beforeId: null,
      });
      const second = hook.result.current.mutateAsync({
        cardId: 'a',
        targetColumnId: 'col-1',
        afterId: null,
        beforeId: null,
      });
      await Promise.all([first, second]);
    });

    expect(bodies).toEqual([
      { id: 'a', expectedVersion: 1 },
      { id: 'a', expectedVersion: 2 },
    ]);
  });

  it('leaves the cache alone when the move cannot be predicted (unknown neighbour) and lets the server decide', async () => {
    server.use(
      http.put(`${API}/cards/a/move`, () =>
        HttpResponse.json(
          { status: 400, code: 'position.neighbor_not_found', detail: 'nope' },
          { status: 400 },
        ),
      ),
    );
    const { hook, cached } = setup([a, b, c]);

    await act(async () => {
      await hook.result.current
        .mutateAsync({ cardId: 'a', targetColumnId: 'col-2', afterId: 'ghost', beforeId: null })
        .catch(() => undefined);
    });

    expect(cached()).toEqual([a, b, c]);
    expect(toast).toBeDefined();
  });
});

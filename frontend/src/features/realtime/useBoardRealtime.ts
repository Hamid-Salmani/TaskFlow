import { HubConnectionBuilder } from '@microsoft/signalr';
import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { API_URL } from '@/shared/api/client';
import { useSessionStore } from '@/shared/store/session-store';
import { boardKeys } from '@/features/workspaces/query-keys';
import { applyBoardEvent } from './apply-board-event';
import { BOARD_EVENT_NAMES, parseBoardEvent } from './board-events';

export type RealtimeStatus = 'connecting' | 'connected' | 'reconnecting' | 'disconnected';

/** The part of `HubConnection` this feature uses, so tests can provide a fake. */
export interface BoardConnection {
  on: (event: string, handler: (payload: unknown) => void) => void;
  start: () => Promise<void>;
  stop: () => Promise<void>;
  invoke: (method: string, ...args: unknown[]) => Promise<unknown>;
  onreconnecting: (handler: () => void) => void;
  onreconnected: (handler: () => void) => void;
  onclose: (handler: () => void) => void;
}

export type ConnectionFactory = () => BoardConnection;

export const createBoardConnection: ConnectionFactory = () =>
  new HubConnectionBuilder()
    .withUrl(`${API_URL}/hubs/board`, {
      // Read at every (re)connect, so a refreshed token is used.
      accessTokenFactory: () => useSessionStore.getState().session?.accessToken ?? '',
    })
    .withAutomaticReconnect()
    .build();

const RETRY_DELAY_MS = 5000;

/**
 * Keeps a SignalR connection to the board: joins its group, applies other users' events to the Query cache,
 * re-joins and refetches after a reconnect (events sent while offline are lost, so the cache is re-synced),
 * and exposes the connection status for the "Reconnecting..." indicator.
 */
export function useBoardRealtime(
  boardId: string,
  connectionFactory: ConnectionFactory = createBoardConnection,
): RealtimeStatus {
  const queryClient = useQueryClient();
  const userId = useSessionStore((state) => state.session?.user.id ?? null);
  const [status, setStatus] = useState<RealtimeStatus>('connecting');

  useEffect(() => {
    let disposed = false;
    let retryTimer: ReturnType<typeof setTimeout> | undefined;
    const connection = connectionFactory();

    for (const name of BOARD_EVENT_NAMES) {
      connection.on(name, (payload) => {
        const event = parseBoardEvent(name, payload);
        if (event !== undefined) {
          applyBoardEvent(queryClient, event, userId);
        }
      });
    }

    const join = () => connection.invoke('JoinBoard', boardId);
    const resync = () => {
      void queryClient.invalidateQueries({ queryKey: boardKeys.detail(boardId) });
      void queryClient.invalidateQueries({ queryKey: boardKeys.cardsOf(boardId) });
    };

    connection.onreconnecting(() => {
      if (!disposed) {
        setStatus('reconnecting');
      }
    });
    connection.onreconnected(() => {
      join()
        .then(() => {
          if (!disposed) {
            resync();
            setStatus('connected');
          }
        })
        .catch(() => !disposed && setStatus('disconnected'));
    });
    connection.onclose(() => {
      if (!disposed) {
        setStatus('disconnected');
      }
    });

    const connect = () => {
      if (disposed) {
        return;
      }
      setStatus('connecting');
      connection
        .start()
        .then(join)
        .then(() => !disposed && setStatus('connected'))
        .catch(() => {
          if (!disposed) {
            setStatus('disconnected');
            retryTimer = setTimeout(connect, RETRY_DELAY_MS);
          }
        });
    };
    connect();

    return () => {
      disposed = true;
      clearTimeout(retryTimer);
      void connection.stop();
    };
  }, [boardId, connectionFactory, queryClient, userId]);

  return status;
}

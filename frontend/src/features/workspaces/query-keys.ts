import type { CardFilters } from '@/features/cards/filters';

export const workspaceKeys = {
  all: ['workspaces'] as const,
  list: () => [...workspaceKeys.all, 'list'] as const,
  detail: (workspaceId: string) => [...workspaceKeys.all, 'detail', workspaceId] as const,
  members: (workspaceId: string) => [...workspaceKeys.all, 'members', workspaceId] as const,
  boards: (workspaceId: string, includeArchived: boolean) =>
    [...workspaceKeys.all, 'boards', workspaceId, { includeArchived }] as const,
  /** Prefix that matches every board list of a workspace regardless of the filter. */
  boardsOf: (workspaceId: string) => [...workspaceKeys.all, 'boards', workspaceId] as const,
};

export const boardKeys = {
  all: ['boards'] as const,
  detail: (boardId: string) => [...boardKeys.all, 'detail', boardId] as const,
  /** Prefix that matches the card lists of a board for every filter combination. */
  cardsOf: (boardId: string) => [...boardKeys.all, 'cards', boardId] as const,
  cards: (boardId: string, filters: CardFilters) =>
    [...boardKeys.cardsOf(boardId), filters] as const,
};

export const cardKeys = {
  all: ['cards'] as const,
  comments: (cardId: string) => [...cardKeys.all, 'comments', cardId] as const,
  activity: (cardId: string) => [...cardKeys.all, 'activity', cardId] as const,
};

export const notificationKeys = {
  all: ['notifications'] as const,
  list: () => [...notificationKeys.all, 'list'] as const,
  unreadCount: () => [...notificationKeys.all, 'unread-count'] as const,
};

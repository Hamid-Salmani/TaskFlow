import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, type RenderResult } from '@testing-library/react';
import type { ReactElement, ReactNode } from 'react';
import { MemoryRouter } from 'react-router-dom';
import { API_BASE_URL } from '@/shared/api/client';
import type { AuthResponse, BoardDetailDto, CardDto, ColumnDto, UserDto } from '@/shared/api/types';
import { useSessionStore } from '@/shared/store/session-store';
import { Toaster } from '@/shared/ui';

/** Base URL that MSW handlers should use, e.g. `http.get(`${API}/boards`, ...)`. */
export const API = API_BASE_URL;

export const testUser: UserDto = {
  id: '11111111-1111-1111-1111-111111111111',
  email: 'ada@test.dev',
  displayName: 'Ada Lovelace',
  handle: 'ada',
};

export function createTestQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: Infinity }, mutations: { retry: false } },
  });
}

export function makeAuthResponse(overrides: Partial<AuthResponse> = {}): AuthResponse {
  return {
    accessToken: 'access-token',
    accessTokenExpiresAt: '2030-01-01T00:00:00Z',
    refreshToken: 'refresh-token',
    user: testUser,
    ...overrides,
  };
}

export function signIn(overrides: Partial<AuthResponse> = {}): AuthResponse {
  const auth = makeAuthResponse(overrides);
  useSessionStore.getState().setSession(auth);
  return auth;
}

export function signOut(): void {
  useSessionStore.getState().clear();
}

interface RenderOptions {
  route?: string;
  queryClient?: QueryClient;
}

export function renderWithProviders(
  ui: ReactElement,
  { route = '/', queryClient = createTestQueryClient() }: RenderOptions = {},
): RenderResult & { queryClient: QueryClient } {
  function Wrapper({ children }: { children: ReactNode }) {
    return (
      <QueryClientProvider client={queryClient}>
        <MemoryRouter initialEntries={[route]}>
          {children}
          <Toaster />
        </MemoryRouter>
      </QueryClientProvider>
    );
  }

  const result = render(ui, { wrapper: Wrapper });
  return Object.assign(result, { queryClient });
}

let cardCounter = 0;

export function makeCard(overrides: Partial<CardDto> = {}): CardDto {
  cardCounter += 1;
  return {
    id: `card-${cardCounter}`,
    boardId: 'board-1',
    columnId: 'col-1',
    title: `Card ${cardCounter}`,
    description: null,
    position: 1024,
    dueDate: null,
    version: 1,
    labelIds: [],
    assigneeIds: [],
    commentCount: 0,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: null,
    ...overrides,
  };
}

export function makeColumn(overrides: Partial<ColumnDto> = {}): ColumnDto {
  return { id: 'col-1', boardId: 'board-1', title: 'To do', position: 1024, ...overrides };
}

export function makeBoard(overrides: Partial<BoardDetailDto> = {}): BoardDetailDto {
  return {
    id: 'board-1',
    workspaceId: 'w1',
    name: 'Roadmap',
    description: null,
    isArchived: false,
    myRole: 'Member',
    columns: [
      makeColumn({ id: 'col-1', title: 'To do', position: 1024 }),
      makeColumn({ id: 'col-2', title: 'Doing', position: 2048 }),
    ],
    labels: [],
    members: [],
    ...overrides,
  };
}

import { expect, type APIRequestContext, type Page } from '@playwright/test';

const API = 'http://localhost:5080/api/v1';
export const PASSWORD = 'Password123!';

export interface ApiUser {
  email: string;
  name: string;
  accessToken: string;
  refreshToken: string;
  user: { id: string; email: string; displayName: string; handle: string };
}

export function uniqueEmail(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 1e6)}@e2e.dev`;
}

export async function registerViaApi(request: APIRequestContext, name: string): Promise<ApiUser> {
  const email = uniqueEmail(name.toLowerCase());
  const response = await request.post(`${API}/auth/register`, {
    data: { email, displayName: name, password: PASSWORD },
  });
  expect(response.ok()).toBeTruthy();
  const auth = (await response.json()) as Omit<ApiUser, 'email' | 'name'>;
  return { email, name, ...auth };
}

/** Starts a page already signed in, using the same storage format as the app's session store. */
export async function signInAs(page: Page, user: ApiUser): Promise<void> {
  await page.addInitScript(
    (session) => {
      window.localStorage.setItem(
        'taskflow-session',
        JSON.stringify({ state: { session }, version: 0 }),
      );
    },
    { accessToken: user.accessToken, refreshToken: user.refreshToken, user: user.user },
  );
}

async function call<T>(
  request: APIRequestContext,
  user: ApiUser,
  method: 'post' | 'put',
  path: string,
  data?: unknown,
): Promise<T> {
  const response = await request[method](`${API}${path}`, {
    data,
    headers: { Authorization: `Bearer ${user.accessToken}` },
  });
  expect(response.ok(), `${method.toUpperCase()} ${path}`).toBeTruthy();
  return (await response.json()) as T;
}

export interface Seeded {
  workspaceId: string;
  boardId: string;
  columns: Array<{ id: string; title: string }>;
  card: { id: string; version: number };
}

/** A workspace with a board (default columns To do / In progress / Done) and one card in the first column. */
export async function seedBoard(
  request: APIRequestContext,
  owner: ApiUser,
  member?: ApiUser,
): Promise<Seeded> {
  const workspace = await call<{ id: string }>(request, owner, 'post', '/workspaces', {
    name: 'E2E workspace',
  });
  if (member) {
    await call(request, owner, 'post', `/workspaces/${workspace.id}/members`, {
      email: member.email,
      role: 'Member',
    });
  }
  const board = await call<{ id: string }>(
    request,
    owner,
    'post',
    `/workspaces/${workspace.id}/boards`,
    { name: 'E2E board' },
  );
  const detail = await (
    await request.get(`${API}/boards/${board.id}`, {
      headers: { Authorization: `Bearer ${owner.accessToken}` },
    })
  ).json();
  const columns = detail.columns as Array<{ id: string; title: string }>;
  const first = columns[0];
  if (!first) {
    throw new Error('The new board has no columns.');
  }
  const card = await call<{ id: string; version: number }>(
    request,
    owner,
    'post',
    `/columns/${first.id}/cards`,
    {
      title: 'Write the report',
    },
  );
  return { workspaceId: workspace.id, boardId: board.id, columns, card };
}

export async function moveCardViaApi(
  request: APIRequestContext,
  user: ApiUser,
  card: { id: string; version: number },
  targetColumnId: string,
): Promise<void> {
  await call(request, user, 'put', `/cards/${card.id}/move`, {
    expectedVersion: card.version,
    targetColumnId,
    afterId: null,
    beforeId: null,
  });
}

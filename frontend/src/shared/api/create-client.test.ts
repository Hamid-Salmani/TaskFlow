import { delay, http, HttpResponse } from 'msw';
import { server } from '@/test/server';
import { createApiClient } from './create-client';
import type { AuthResponse } from './types';

const BASE = 'http://localhost:5080/api/v1';

function authResponse(generation: number): AuthResponse {
  return {
    accessToken: `access-${generation}`,
    accessTokenExpiresAt: '2030-01-01T00:00:00Z',
    refreshToken: `refresh-${generation}`,
    user: { id: 'u1', email: 'ada@test.dev', displayName: 'Ada', handle: 'ada' },
  };
}

function setup() {
  const tokens = { access: 'access-0', refresh: 'refresh-0' };
  const onAuthFailure = vi.fn();
  const client = createApiClient({
    baseURL: BASE,
    getAccessToken: () => tokens.access,
    getRefreshToken: () => tokens.refresh,
    onTokensRefreshed: (auth) => {
      tokens.access = auth.accessToken;
      tokens.refresh = auth.refreshToken;
    },
    onAuthFailure,
  });

  const refresh = { calls: 0, bodies: [] as unknown[], fail: false };
  server.use(
    http.post(`${BASE}/auth/refresh`, async ({ request }) => {
      refresh.calls += 1;
      refresh.bodies.push(await request.json());
      await delay(40); // long enough for the parallel requests below to pile up
      if (refresh.fail) {
        return HttpResponse.json(
          { status: 401, code: 'auth.refresh_token_reused' },
          { status: 401 },
        );
      }
      return HttpResponse.json(authResponse(refresh.calls));
    }),
  );

  return { client, tokens, onAuthFailure, refresh };
}

/** A protected endpoint that only accepts the token from the first refresh. */
function useProtectedEndpoint(path: string, options: { delayMs?: number } = {}) {
  const seen: Array<string | null> = [];
  server.use(
    http.get(`${BASE}${path}`, async ({ request }) => {
      const header = request.headers.get('authorization');
      seen.push(header);
      if (options.delayMs) {
        await delay(options.delayMs);
      }
      return header === 'Bearer access-1'
        ? HttpResponse.json({ ok: true })
        : HttpResponse.json({ status: 401 }, { status: 401 });
    }),
  );
  return seen;
}

describe('api client', () => {
  it('attaches the access token to requests', async () => {
    const { client } = setup();
    let header: string | null = null;
    server.use(
      http.get(`${BASE}/ping`, ({ request }) => {
        header = request.headers.get('authorization');
        return HttpResponse.json({});
      }),
    );

    await client.get('/ping');

    expect(header).toBe('Bearer access-0');
  });

  it('refreshes once for several parallel 401s and replays every request', async () => {
    const { client, tokens, refresh, onAuthFailure } = setup();
    const seen = useProtectedEndpoint('/things');

    const responses = await Promise.all([
      client.get('/things'),
      client.get('/things'),
      client.get('/things'),
      client.get('/things'),
    ]);

    expect(responses.map((response) => response.status)).toEqual([200, 200, 200, 200]);
    expect(refresh.calls).toBe(1);
    expect(refresh.bodies).toEqual([{ refreshToken: 'refresh-0' }]);
    expect(tokens).toEqual({ access: 'access-1', refresh: 'refresh-1' });
    expect(seen.filter((header) => header === 'Bearer access-0')).toHaveLength(4);
    expect(seen.filter((header) => header === 'Bearer access-1')).toHaveLength(4);
    expect(onAuthFailure).not.toHaveBeenCalled();
  });

  it('replays a late 401 with the already refreshed token instead of refreshing again', async () => {
    const { client, refresh } = setup();
    useProtectedEndpoint('/fast');
    useProtectedEndpoint('/slow', { delayMs: 200 });

    const [fast, slow] = await Promise.all([client.get('/fast'), client.get('/slow')]);

    expect(fast.status).toBe(200);
    expect(slow.status).toBe(200);
    expect(refresh.calls).toBe(1);
  });

  it('signs out and rejects with the original 401 when the refresh fails', async () => {
    const { client, refresh, onAuthFailure } = setup();
    refresh.fail = true;
    useProtectedEndpoint('/things');

    await expect(client.get('/things')).rejects.toMatchObject({ response: { status: 401 } });

    expect(onAuthFailure).toHaveBeenCalledTimes(1);
  });

  it('fails every queued request when the shared refresh fails, with a single sign-out per request', async () => {
    const { client, refresh } = setup();
    refresh.fail = true;
    useProtectedEndpoint('/things');

    const results = await Promise.allSettled([client.get('/things'), client.get('/things')]);

    expect(results.map((result) => result.status)).toEqual(['rejected', 'rejected']);
    expect(refresh.calls).toBe(1);
  });

  it('retries a request at most once', async () => {
    const { client, refresh } = setup();
    server.use(http.get(`${BASE}/always-401`, () => HttpResponse.json({}, { status: 401 })));

    await expect(client.get('/always-401')).rejects.toMatchObject({ response: { status: 401 } });

    expect(refresh.calls).toBe(1);
  });

  it('does not treat an error of the replayed request as a failed session', async () => {
    const { client, onAuthFailure } = setup();
    server.use(
      http.get(`${BASE}/secret`, ({ request }) =>
        request.headers.get('authorization') === 'Bearer access-1'
          ? HttpResponse.json({ status: 403 }, { status: 403 })
          : HttpResponse.json({}, { status: 401 }),
      ),
    );

    await expect(client.get('/secret')).rejects.toMatchObject({ response: { status: 403 } });

    expect(onAuthFailure).not.toHaveBeenCalled();
  });

  it('skipAuth requests carry no token and never trigger a refresh', async () => {
    const { client, refresh } = setup();
    let header: string | null = 'unset';
    server.use(
      http.post(`${BASE}/auth/login`, ({ request }) => {
        header = request.headers.get('authorization');
        return HttpResponse.json({ code: 'auth.invalid_credentials' }, { status: 401 });
      }),
    );

    await expect(client.post('/auth/login', {}, { skipAuth: true })).rejects.toMatchObject({
      response: { status: 401 },
    });

    expect(header).toBeNull();
    expect(refresh.calls).toBe(0);
  });

  it('passes non-401 errors through untouched', async () => {
    const { client, refresh } = setup();
    server.use(http.get(`${BASE}/boom`, () => HttpResponse.json({}, { status: 500 })));

    await expect(client.get('/boom')).rejects.toMatchObject({ response: { status: 500 } });

    expect(refresh.calls).toBe(0);
  });

  it('does not try to refresh without a refresh token', async () => {
    const onAuthFailure = vi.fn();
    const client = createApiClient({
      baseURL: BASE,
      getAccessToken: () => null,
      getRefreshToken: () => null,
      onTokensRefreshed: vi.fn(),
      onAuthFailure,
    });
    server.use(http.get(`${BASE}/things`, () => HttpResponse.json({}, { status: 401 })));

    await expect(client.get('/things')).rejects.toMatchObject({ response: { status: 401 } });

    expect(onAuthFailure).not.toHaveBeenCalled();
  });
});

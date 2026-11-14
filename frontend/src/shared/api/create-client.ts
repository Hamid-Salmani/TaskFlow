import axios, { isAxiosError, type AxiosInstance } from 'axios';
import type { AuthResponse } from './types';

declare module 'axios' {
  interface AxiosRequestConfig {
    /** Do not attach the access token and never try to refresh on 401 (login, register, refresh). */
    skipAuth?: boolean;
  }

  interface InternalAxiosRequestConfig {
    /** Internal marker: this request was already replayed once after a token refresh. */
    retried?: boolean;
  }
}

export interface ApiClientOptions {
  baseURL: string;
  getAccessToken: () => string | null;
  getRefreshToken: () => string | null;
  onTokensRefreshed: (auth: AuthResponse) => void;
  onAuthFailure: () => void;
}

function bearerToken(header: unknown): string | null {
  return typeof header === 'string' && header.startsWith('Bearer ') ? header.slice(7) : null;
}

/**
 * Axios instance that
 *  1. attaches the access token,
 *  2. on 401 refreshes the session **once** no matter how many requests fail at the same time
 *     (single flight: later 401s await the same promise = the "queue"), then replays each request,
 *  3. reports an unrecoverable session through `onAuthFailure`.
 */
export function createApiClient(options: ApiClientOptions): AxiosInstance {
  const client = axios.create({ baseURL: options.baseURL });
  // Separate instance without interceptors, otherwise a failing refresh would try to refresh itself.
  const refreshClient = axios.create({ baseURL: options.baseURL });

  let inFlightRefresh: Promise<string> | null = null;

  async function refreshTokens(): Promise<string> {
    const refreshToken = options.getRefreshToken();
    if (!refreshToken) {
      throw new Error('No refresh token available.');
    }

    const { data } = await refreshClient.post<AuthResponse>('/auth/refresh', { refreshToken });
    options.onTokensRefreshed(data);
    return data.accessToken;
  }

  function refreshOnce(): Promise<string> {
    inFlightRefresh ??= refreshTokens().finally(() => {
      inFlightRefresh = null;
    });
    return inFlightRefresh;
  }

  client.interceptors.request.use((config) => {
    if (config.skipAuth) {
      return config;
    }

    const token = options.getAccessToken();
    if (token) {
      config.headers.set('Authorization', `Bearer ${token}`);
    }
    return config;
  });

  client.interceptors.response.use(
    (response) => response,
    async (error: unknown) => {
      if (!isAxiosError(error) || !error.config || error.response?.status !== 401) {
        throw error;
      }

      const config = error.config;
      if (config.skipAuth || config.retried || options.getRefreshToken() === null) {
        throw error;
      }

      config.retried = true;
      const sentToken = bearerToken(config.headers.get('Authorization'));
      const currentToken = options.getAccessToken();

      let token: string;
      try {
        // If another request already refreshed while this one was in flight, just replay with the new token.
        token =
          currentToken !== null && currentToken !== sentToken ? currentToken : await refreshOnce();
      } catch {
        options.onAuthFailure();
        throw error;
      }

      config.headers.set('Authorization', `Bearer ${token}`);
      // Errors of the replayed request (403, 404, ...) are normal API errors, not an auth failure.
      return client.request(config);
    },
  );

  return client;
}

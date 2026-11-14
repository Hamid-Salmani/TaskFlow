import type { AxiosRequestConfig } from 'axios';
import { api } from './client';
import { toApiError } from './errors';

/**
 * Sends a request through the shared client and normalizes failures to `ApiError`, so every hook rejects
 * with one error type. `T` is the response body (use `void` for 204 responses).
 */
export async function request<T>(config: AxiosRequestConfig): Promise<T> {
  try {
    const { data } = await api.request<T>(config);
    return data;
  } catch (error) {
    throw toApiError(error);
  }
}

import { useMutation } from '@tanstack/react-query';
import { api } from '@/shared/api/client';
import { toApiError, type ApiError } from '@/shared/api/errors';
import type { AuthResponse, LoginRequest } from '@/shared/api/types';
import { useSessionStore } from '@/shared/store/session-store';

async function login(request: LoginRequest): Promise<AuthResponse> {
  try {
    const { data } = await api.post<AuthResponse>('/auth/login', request, { skipAuth: true });
    return data;
  } catch (error) {
    throw toApiError(error);
  }
}

export function useLogin() {
  const setSession = useSessionStore((state) => state.setSession);
  return useMutation<AuthResponse, ApiError, LoginRequest>({
    mutationFn: login,
    onSuccess: setSession,
  });
}

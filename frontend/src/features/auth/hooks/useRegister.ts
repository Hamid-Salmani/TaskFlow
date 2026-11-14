import { useMutation } from '@tanstack/react-query';
import { api } from '@/shared/api/client';
import { toApiError, type ApiError } from '@/shared/api/errors';
import type { AuthResponse, RegisterRequest } from '@/shared/api/types';
import { useSessionStore } from '@/shared/store/session-store';

async function register(request: RegisterRequest): Promise<AuthResponse> {
  try {
    const { data } = await api.post<AuthResponse>('/auth/register', request, { skipAuth: true });
    return data;
  } catch (error) {
    throw toApiError(error);
  }
}

export function useRegister() {
  const setSession = useSessionStore((state) => state.setSession);
  return useMutation<AuthResponse, ApiError, RegisterRequest>({
    mutationFn: register,
    onSuccess: setSession,
  });
}

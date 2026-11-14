import { isAxiosError } from 'axios';

/** Normalized API failure (RFC 9457 problem details, see NOTES.md) or a network problem. */
export class ApiError extends Error {
  readonly status: number | null;
  readonly code: string | null;
  readonly fieldErrors: Record<string, string[]>;

  constructor(
    message: string,
    options: {
      status?: number | null;
      code?: string | null;
      fieldErrors?: Record<string, string[]>;
    } = {},
  ) {
    super(message);
    this.name = 'ApiError';
    this.status = options.status ?? null;
    this.code = options.code ?? null;
    this.fieldErrors = options.fieldErrors ?? {};
  }

  get isConflict(): boolean {
    return this.status === 409;
  }

  get isNetworkError(): boolean {
    return this.status === null;
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}

function readFieldErrors(value: unknown): Record<string, string[]> {
  if (!isRecord(value)) {
    return {};
  }

  const result: Record<string, string[]> = {};
  for (const [field, messages] of Object.entries(value)) {
    if (Array.isArray(messages)) {
      result[field] = messages.filter((message): message is string => typeof message === 'string');
    }
  }
  return result;
}

export function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) {
    return error;
  }

  if (isAxiosError(error)) {
    if (!error.response) {
      return new ApiError('Could not reach the server. Check your connection and try again.');
    }

    const body: unknown = error.response.data;
    if (isRecord(body)) {
      const detail = typeof body.detail === 'string' ? body.detail : null;
      const title = typeof body.title === 'string' ? body.title : null;
      return new ApiError(detail ?? title ?? error.message, {
        status: error.response.status,
        code: typeof body.code === 'string' ? body.code : null,
        fieldErrors: readFieldErrors(body.errors),
      });
    }

    return new ApiError(error.message, { status: error.response.status });
  }

  return new ApiError(error instanceof Error ? error.message : 'Something went wrong.');
}

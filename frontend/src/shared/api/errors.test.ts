import { AxiosError, AxiosHeaders, type AxiosResponse } from 'axios';
import { ApiError, toApiError } from './errors';

function axiosErrorWith(status: number, data: unknown): AxiosError {
  const response: AxiosResponse = {
    status,
    statusText: '',
    data,
    headers: {},
    config: { headers: new AxiosHeaders() },
  };
  return new AxiosError('Request failed', 'ERR_BAD_REQUEST', response.config, null, response);
}

describe('toApiError', () => {
  it('reads detail, code and field errors from problem details', () => {
    const error = toApiError(
      axiosErrorWith(400, {
        title: 'Bad Request',
        detail: 'One or more validation errors occurred.',
        code: 'validation.failed',
        errors: { email: ['Email is invalid'], broken: 'not-an-array' },
      }),
    );

    expect(error.status).toBe(400);
    expect(error.code).toBe('validation.failed');
    expect(error.message).toBe('One or more validation errors occurred.');
    expect(error.fieldErrors).toEqual({ email: ['Email is invalid'] });
  });

  it('flags conflicts', () => {
    expect(toApiError(axiosErrorWith(409, { code: 'card.version_conflict' })).isConflict).toBe(
      true,
    );
  });

  it('falls back to the title when there is no detail', () => {
    expect(toApiError(axiosErrorWith(404, { title: 'Not Found' })).message).toBe('Not Found');
  });

  it('describes network errors without a status', () => {
    const error = toApiError(new AxiosError('Network Error'));

    expect(error.isNetworkError).toBe(true);
    expect(error.message).toMatch(/could not reach the server/i);
  });

  it('keeps the status for non-JSON error bodies', () => {
    expect(toApiError(axiosErrorWith(502, '<html>Bad gateway</html>')).status).toBe(502);
  });

  it('returns ApiError instances unchanged and wraps unknown errors', () => {
    const original = new ApiError('x', { status: 400 });

    expect(toApiError(original)).toBe(original);
    expect(toApiError(new Error('boom')).message).toBe('boom');
    expect(toApiError('weird').message).toBe('Something went wrong.');
  });
});

import { setupServer } from 'msw/node';

/** Shared MSW server. Tests register the handlers they need with `server.use(...)`. */
export const server = setupServer();

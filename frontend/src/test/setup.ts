import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterAll, afterEach, beforeAll, vi } from 'vitest';
import { server } from './server';

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));

afterEach(() => {
  cleanup();
  server.resetHandlers();
  localStorage.clear();
  document.documentElement.classList.remove('dark');
});

afterAll(() => server.close());

// jsdom does not implement matchMedia; the theme hook needs it.
Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: vi.fn().mockImplementation((query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    addListener: vi.fn(),
    removeListener: vi.fn(),
    dispatchEvent: vi.fn(),
  })),
});

Element.prototype.scrollIntoView = vi.fn();

// Components never open a real SignalR connection in unit tests; the realtime hook is tested with a fake connection.
vi.mock('@microsoft/signalr', () => {
  class IdleConnection {
    on = vi.fn();
    start = vi.fn(() => Promise.resolve());
    stop = vi.fn(() => Promise.resolve());
    invoke = vi.fn(() => Promise.resolve());
    onreconnecting = vi.fn();
    onreconnected = vi.fn();
    onclose = vi.fn();
  }
  class HubConnectionBuilder {
    withUrl() {
      return this;
    }
    withAutomaticReconnect() {
      return this;
    }
    build() {
      return new IdleConnection();
    }
  }
  return { HubConnectionBuilder };
});

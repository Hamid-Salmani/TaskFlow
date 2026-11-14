import { defineConfig, devices } from '@playwright/test';

const API_PORT = 5080;
const WEB_PORT = 5173;
const DATABASE = process.env['E2E_DATABASE'] ?? '/tmp/taskflow-e2e.db';

/**
 * E2E tests run against the REAL stack: the ASP.NET Core API on a throw-away SQLite file and the Vite dev server.
 * Both are started (and stopped) by Playwright. Locally an already running pair is reused.
 */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: false, // one shared database; the tests create their own users and never touch each other's data
  workers: 1,
  retries: process.env['CI'] ? 1 : 0,
  reporter: process.env['CI'] ? [['github'], ['html', { open: 'never' }]] : 'list',
  timeout: 45_000,
  expect: { timeout: 10_000 },
  use: {
    baseURL: `http://localhost:${WEB_PORT}`,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    // CI installs Playwright's own Chromium; a sandbox can point this at a pre-installed one.
    ...(process.env['E2E_CHROMIUM']
      ? { launchOptions: { executablePath: process.env['E2E_CHROMIUM'] } }
      : {}),
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: [
    {
      command: `rm -f "${DATABASE}" && dotnet run --project ../backend/src/TaskFlow.Api --no-launch-profile --urls http://localhost:${API_PORT}`,
      url: `http://localhost:${API_PORT}/health`,
      reuseExistingServer: !process.env['CI'],
      timeout: 120_000,
      env: {
        ASPNETCORE_ENVIRONMENT: 'Development',
        ConnectionStrings__Default: `Data Source=${DATABASE}`,
        Seed__Enabled: 'false',
        RateLimiting__AuthPermitLimit: '1000',
      },
    },
    {
      command: `npm run dev -- --port ${WEB_PORT} --strictPort`,
      url: `http://localhost:${WEB_PORT}`,
      reuseExistingServer: !process.env['CI'],
      timeout: 60_000,
    },
  ],
});

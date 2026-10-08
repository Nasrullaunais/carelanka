import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: '.',
  outputDir: 'results/artifacts',
  timeout: 300_000,
  expect: { timeout: 15_000 },
  workers: 1,
  reporter: [['list'], ['html', { outputFolder: 'results/html-report', open: 'never' }]],
  use: {
    trace: 'on',
    screenshot: 'on',
    // -Headed slows each action down so people watching can follow it.
    launchOptions: { slowMo: Number(process.env.E2E_SLOW_MO ?? 0) },
  },
  // Starts the React app if it is not already running.
  webServer: {
    command: 'npm run dev',
    cwd: '../../../web-ui',
    url: 'http://localhost:5174',
    reuseExistingServer: true,
    timeout: 120_000,
  },
});

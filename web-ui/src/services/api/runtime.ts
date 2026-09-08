import type { CreateClientConfig } from './generated/client.gen';

// The generated client calls createClientConfig while it is still initialising, so a real
// import back into generated code from here is a startup crash. The one import above is
// `import type`, which TypeScript erases — nothing of it survives to run.
//
// Everything else about HTTP — the token header, the failure toasts — lives in transport.ts,
// which runs after the client exists.
export const createClientConfig: CreateClientConfig = (config) => ({
  ...config,
  // Relative in dev and production alike. Vite proxies /api to the backend in dev; in
  // production the API serves the app. No base-URL environment variable, deliberately:
  // it buys a CORS surface and an environment contract for nothing.
  baseUrl: '/api',
});

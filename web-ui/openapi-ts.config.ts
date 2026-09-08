import { defineConfig } from '@hey-api/openapi-ts';

// The ASP.NET API is the source of truth. Never hand-write a fetch() or a model for a
// CareLanka endpoint — regenerate this and import it. Run the API first: it publishes the
// document this reads.
export default defineConfig({
  input: 'http://localhost:5231/swagger/v1/swagger.json',
  output: {
    path: 'src/services/api/generated',
    postProcess: [],
  },
  plugins: [
    {
      name: '@hey-api/client-fetch',
      // Drops the spec's servers entry, which names whichever host published the document.
      // The base URL is set in runtime.ts instead, as the relative path /api.
      baseUrl: false,
      runtimeConfigPath: './src/services/api/runtime.ts',
    },
    '@tanstack/react-query',
  ],
});

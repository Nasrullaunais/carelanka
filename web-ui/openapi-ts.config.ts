import { defineConfig } from '@hey-api/openapi-ts';

export default defineConfig({
  input: 'http://localhost:5231/swagger/v1/swagger.json',
  output: {
    path: 'src/services/api/generated',
    postProcess: [],
  },
  plugins: [
    {
      name: '@hey-api/client-fetch',
      baseUrl: false,
      runtimeConfigPath: './src/services/api/runtime.ts',
    },
    '@tanstack/react-query',
  ],
});

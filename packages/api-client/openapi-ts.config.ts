import { defineConfig } from '@hey-api/openapi-ts';

export default defineConfig({
  input: '../../apps/api/openapi/MouseDent.Api.json',
  output: {
    path: 'src/generated',
    postProcess: ['prettier'],
  },
  plugins: ['@hey-api/client-fetch'],
});

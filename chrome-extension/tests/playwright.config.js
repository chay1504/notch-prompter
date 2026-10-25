import {defineConfig} from '@playwright/test';
export default defineConfig({
  testMatch: 'extension.spec.js', timeout: 40000, workers: 1, reporter: 'list',
  use: {headless: true},
  webServer: {command:'node server.cjs', url:'http://127.0.0.1:8794', reuseExistingServer:false}
});

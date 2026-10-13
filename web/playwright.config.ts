import fs from 'node:fs';
import path from 'node:path';
import { defineConfig, devices } from '@playwright/test';
import type { Lang } from './e2e/fixtures';

// Local runs read the generated test password and URLs from ../.dev.env (git-ignored); CI sets them as secrets.
const devEnv = path.resolve(__dirname, '../.dev.env');
if (fs.existsSync(devEnv)) {
  for (const line of fs.readFileSync(devEnv, 'utf8').split(/\r?\n/)) {
    const m = /^﻿?([A-Za-z0-9_]+)=(.*)$/.exec(line);
    if (m && !process.env[m[1]]) process.env[m[1]] = m[2];
  }
}
process.env.E2E_API_URL ??= 'http://localhost:5080';
process.env.E2E_WEB_URL ??= 'http://localhost:3000';

export default defineConfig<{ lang: Lang }>({
  testDir: './e2e',
  fullyParallel: false,            // tests share one seeded database
  workers: 1,
  retries: process.env.CI ? 1 : 0,
  timeout: 45_000,
  expect: { timeout: 10_000 },
  reporter: [['list'], ['html', { open: 'never' }]],
  use: { baseURL: process.env.E2E_WEB_URL, trace: 'retain-on-failure' },
  projects: [
    { name: 'bn-mobile', use: { ...devices['Pixel 7'], lang: 'bn' }, testIgnore: '**/journeys/**' },
    { name: 'bn-desktop', use: { viewport: { width: 1280, height: 900 }, lang: 'bn' } },
    { name: 'en-desktop', use: { viewport: { width: 1280, height: 900 }, lang: 'en' }, testIgnore: '**/journeys/**' },
  ],
});

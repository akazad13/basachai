// Extracted from specs/tech/quality-and-testing.md by scripts/extract-e2e.mjs. Edit the spec, then re-extract.
// web/e2e/fixtures.ts
import { test as base, expect, type APIRequestContext } from '@playwright/test';
import bn from '../messages/bn.json';
import en from '../messages/en.json';
import { USERS } from './seed-data';

export type Lang = 'bn' | 'en';
type Role = keyof typeof USERS;
const MESSAGES: Record<Lang, Record<string, string>> = { bn, en };
const API = process.env.E2E_API_URL ?? 'http://localhost:5080';

type Fixtures = {
  lang: Lang;
  t: (key: string, vars?: Record<string, string | number>) => string;
  num: (n: number) => string;
  money: (n: number) => string;
  api: APIRequestContext;
  seed: (dataset?: 'pilot' | 'empty') => Promise<void>;
  clock: (offsetDays: number) => Promise<void>;
  act: (listingId: string, action: string, data?: Record<string, unknown>) => Promise<void>;
  signInAs: (role: Role) => Promise<void>;
  journeyTime: void;
};

export const test = base.extend<Fixtures>({
  lang: ['bn', { option: true }],
  // Journeys drive several actors through a whole flow: 2 minutes instead of the 45 s every other test gets.
  journeyTime: [async ({}, use, info) => {
    if (info.tags.includes('@journey')) info.setTimeout(120_000);
    await use();
  }, { auto: true }],
  t: async ({ lang }, use) => {
    await use((key, vars = {}) =>
      Object.entries(vars).reduce((s, [k, v]) => s.replaceAll(`{${k}}`, String(v)), MESSAGES[lang][key] ?? key));
  },
  num: async ({ lang }, use) => {
    const f = new Intl.NumberFormat(lang === 'bn' ? 'bn-BD' : 'en-IN');
    await use(n => f.format(n));
  },
  money: async ({ num }, use) => { await use(n => `৳${num(n)}`); },
  api: async ({ playwright }, use) => {
    // Staging's test hooks need the hook key (Test:HookKey); local and CI runs leave E2E_TEST_KEY unset.
    const key = process.env.E2E_TEST_KEY;
    const ctx = await playwright.request.newContext({ baseURL: API, extraHTTPHeaders: key ? { 'X-Test-Key': key } : {} });
    await use(ctx);
    await ctx.dispose();
  },
  seed: async ({ api }, use) => {
    await use(async (dataset = 'pilot') => {
      expect((await api.post('/v1/test/seed', { data: { dataset } })).ok()).toBeTruthy();
    });
  },
  clock: async ({ api }, use) => {
    await use(async offsetDays => {
      expect((await api.post('/v1/test/clock', { data: { offsetDays } })).ok()).toBeTruthy();
    });
  },
  act: async ({ api }, use) => {
    await use(async (listingId, action, data) => {
      expect((await api.post('/v1/test/act', { data: { listingId, action, data } })).ok()).toBeTruthy();
    });
  },
  signInAs: async ({ page }, use) => {
    // page.request shares cookies with the page, so the browser is signed in afterwards.
    await use(async role => {
      const res = await page.request.post(`${API}/v1/auth/login`, {
        data: { phone: USERS[role].phone, password: process.env.E2E_PASSWORD },
      });
      expect(res.ok()).toBeTruthy();
    });
  },
});

export { expect };

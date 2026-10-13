// Extracted from specs/journey.md by scripts/extract-e2e.mjs. Edit the spec, then re-extract.
// web/e2e/journeys/actors.ts
import { devices, type Browser, type Page } from '@playwright/test';
import { USERS } from '../seed-data';

const API = process.env.E2E_API_URL!;

export async function actor(browser: Browser, opts: { role?: keyof typeof USERS; mobile?: boolean } = {}): Promise<Page> {
  const context = await browser.newContext({
    baseURL: process.env.E2E_WEB_URL,
    ...(opts.mobile ? devices['Pixel 7'] : { viewport: { width: 1280, height: 900 } }),
  });
  const page = await context.newPage();
  if (opts.role) {
    const res = await page.request.post(`${API}/v1/auth/login`, {
      data: { phone: USERS[opts.role].phone, password: process.env.E2E_PASSWORD },
    });
    if (!res.ok()) throw new Error(`Sign-in failed for ${opts.role}`);
  }
  return page;
}

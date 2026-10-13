import { defineRouting } from 'next-intl/routing';

/** Bangla first (D1); English through the header toggle. Every route is prefixed: /bn/..., /en/... */
export const routing = defineRouting({
  locales: ['bn', 'en'],
  defaultLocale: 'bn',
  localePrefix: 'always',
  // Bangla first for everyone: an English browser is not redirected to /en. The toggle (and its cookie) decides after that.
  localeDetection: false,
});

export type Locale = (typeof routing.locales)[number];

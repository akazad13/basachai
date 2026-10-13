import type { NextConfig } from 'next';
import createNextIntlPlugin from 'next-intl/plugin';

const withNextIntl = createNextIntlPlugin('./i18n/request.ts');

const api = new URL(process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5080').origin;
const dev = process.env.NODE_ENV !== 'production';

/**
 * Content Security Policy (security spec). Next.js inlines its bootstrap scripts, so scripts need 'unsafe-inline'
 * until nonces are added; everything else is limited to this site, the API and the photo hosts.
 */
const csp = [
  "default-src 'self'",
  `script-src 'self' 'unsafe-inline'${dev ? " 'unsafe-eval'" : ''}`,
  "style-src 'self' 'unsafe-inline'",
  `img-src 'self' data: blob: https://res.cloudinary.com https://images.unsplash.com ${api}`,
  "font-src 'self'",
  `connect-src 'self' ${api}${dev ? ' ws:' : ''}`,
  "object-src 'none'",
  "base-uri 'self'",
  "form-action 'self'",
  "frame-ancestors 'none'",
].join('; ');

const nextConfig: NextConfig = {
  // Cloudinary resizes photos; Next.js image optimisation stays off so Netlify does not bill for it.
  images: { unoptimized: true },
  // Cached pages are refreshed in the background after 60 s; after 5 minutes without visits they are rendered fresh
  // rather than served stale, which bounds changes no purge covers (a listing passing its expiry time).
  expireTime: 300,
  poweredByHeader: false,
  async headers() {
    return [{
      source: '/:path*',
      headers: [
        { key: 'Content-Security-Policy', value: csp },
        { key: 'X-Content-Type-Options', value: 'nosniff' },
        { key: 'Referrer-Policy', value: 'strict-origin-when-cross-origin' },
        { key: 'X-Frame-Options', value: 'DENY' },
        { key: 'Permissions-Policy', value: 'camera=(), microphone=(), geolocation=()' },
      ],
    }];
  },
};

export default withNextIntl(nextConfig);

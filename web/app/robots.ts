import type { MetadataRoute } from 'next';

const SITE = process.env.NEXT_PUBLIC_SITE_URL ?? 'http://localhost:3000';

/** Public pages are indexable; signed-in and staff pages are not. */
export default function robots(): MetadataRoute.Robots {
  const privatePaths = ['account', 'saved', 'dashboard', 'post', 'admin', 'reset'].flatMap(p => [`/bn/${p}`, `/en/${p}`]);
  return { rules: { userAgent: '*', allow: '/', disallow: privatePaths }, sitemap: `${SITE}/sitemap.xml` };
}

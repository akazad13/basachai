import type { MetadataRoute } from 'next';
import { serverGet, TAGS } from '@/lib/api';
import type { Card, Locations, SearchPage } from '@/lib/types';

const SITE = process.env.NEXT_PUBLIC_SITE_URL ?? 'http://localhost:3000';
const LANGS = ['bn', 'en'] as const;

// Regenerated hourly (SEO spec). Only Live listings are listed, so unavailable ones drop out.
export const revalidate = 3600;

/** Every Live listing, page by page. "none" keeps these reads out of the search analytics. */
async function liveListings(): Promise<Card[]> {
  const cards: Card[] = [];
  let cursor: string | null = null;
  for (let i = 0; i < 200; i++) {
    const page: SearchPage = await serverGet<SearchPage>(`/v1/listings?sort=newest&size=24${cursor ? '&cursor=' + encodeURIComponent(cursor) : ''}`,
      { headers: { 'X-Search-Source': 'none' }, revalidate: 3600, tags: [TAGS.listings] });
    cards.push(...page.items);
    cursor = page.nextCursor;
    if (!cursor) break;
  }
  return cards;
}

function entry(path: (lang: string) => string, lastModified?: string): MetadataRoute.Sitemap[number] {
  return {
    url: SITE + path('bn'),
    lastModified,
    alternates: { languages: Object.fromEntries(LANGS.map(l => [l, SITE + path(l)])) },
  };
}

export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  // The build may not reach the API: it writes the fixed pages only, and the first refresh (or listing purge) fills
  // in the rest. At run time failures throw, so an outage never replaces the sitemap with an empty one for an hour.
  const building = process.env.NEXT_PHASE === 'phase-production-build';
  const [areas, cards] = await Promise.all([
    serverGet<Locations>('/v1/locations', { revalidate: 3600, tags: [TAGS.reference] }).then(l => l.areas, e => { if (building) return []; throw e; }),
    liveListings().catch(e => { if (building) return [] as Card[]; throw e; }),
  ]);
  return [
    entry(l => `/${l}`),
    ...['how-it-works', 'safety', 'terms', 'privacy'].map(p => entry(l => `/${l}/${p}`)),
    ...areas.map(a => entry(l => `/${l}/rent/dhaka/dhamrai/${a.slug}`)),
    ...cards.map(c => entry(l => `/${l}/rent/dhaka/dhamrai/${c.area.slug}/${c.slug}-${c.shortId}`, c.lastConfirmedAt ?? undefined)),
  ];
}

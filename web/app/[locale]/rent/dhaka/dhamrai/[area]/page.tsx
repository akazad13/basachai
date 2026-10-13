import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { getTranslations, setRequestLocale } from 'next-intl/server';
import { PAGE_SECONDS, serverGet, TAGS } from '@/lib/api';
import type { Lang } from '@/lib/format';
import type { Locations } from '@/lib/types';
import { areaName } from '@/components/listing-card';
import { filtersOf, loadSearch } from '@/app/[locale]/search/load';
import { SearchView } from '@/app/[locale]/search/search-view';

/**
 * Area results for search engines: /bn/rent/dhaka/dhamrai/{area} (SEO table). Served from the edge for 60 seconds and
 * purged when listings change. The page shows the area only; changing a filter continues on /search.
 */
export const revalidate = 60;
export function generateStaticParams() { return []; }   // rendered on first request, then cached

type Params = { params: Promise<{ locale: string; area: string }> };

const SITE = process.env.NEXT_PUBLIC_SITE_URL ?? 'http://localhost:3000';

export async function generateMetadata({ params }: Params): Promise<Metadata> {
  const { locale, area } = await params;
  const t = await getTranslations({ locale });
  const found = await serverGet<Locations>('/v1/locations', { revalidate: PAGE_SECONDS, tags: [TAGS.reference] })
    .then(l => l.areas.find(a => a.slug === area), () => undefined);
  const path = (lang: string) => `${SITE}/${lang}/rent/dhaka/dhamrai/${area}`;
  return {
    title: t('homes_in', { a: found ? areaName(locale as Lang, found) : t('all_dhamrai') }),
    alternates: { canonical: path(locale), languages: { bn: path('bn'), en: path('en') } },
  };
}

export default async function AreaPage({ params }: Params) {
  const { locale, area } = await params;
  setRequestLocale(locale);
  if (!/^[a-z0-9_-]{1,40}$/.test(area)) notFound();
  const known = await serverGet<Locations>('/v1/locations', { revalidate: PAGE_SECONDS, tags: [TAGS.reference] });
  if (!known.areas.some(a => a.slug === area)) notFound();
  const filters = filtersOf({}, area);
  const { initial, areas } = await loadSearch(filters, true);
  return <SearchView initial={initial} areas={areas} filters={filters} area={area} />;
}

import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { getTranslations, setRequestLocale } from 'next-intl/server';
import { serverGet } from '@/lib/api';
import type { Lang } from '@/lib/format';
import type { Locations } from '@/lib/types';
import { areaName } from '@/components/listing-card';
import { loadSearch } from '@/app/[locale]/search/load';
import { SearchView } from '@/app/[locale]/search/search-view';

/** Area results for search engines: /bn/rent/dhaka/dhamrai/{area} (SEO table). Changing a filter continues on /search. */
export const dynamic = 'force-dynamic';

type Params = { params: Promise<{ locale: string; area: string }>; searchParams: Promise<Record<string, string | string[] | undefined>> };

const SITE = process.env.NEXT_PUBLIC_SITE_URL ?? 'http://localhost:3000';

export async function generateMetadata({ params }: Params): Promise<Metadata> {
  const { locale, area } = await params;
  const t = await getTranslations({ locale });
  const found = await serverGet<Locations>('/v1/locations').then(l => l.areas.find(a => a.slug === area), () => undefined);
  const path = (lang: string) => `${SITE}/${lang}/rent/dhaka/dhamrai/${area}`;
  return {
    title: t('homes_in', { a: found ? areaName(locale as Lang, found) : t('all_dhamrai') }),
    alternates: { canonical: path(locale), languages: { bn: path('bn'), en: path('en') } },
  };
}

export default async function AreaPage({ params, searchParams }: Params) {
  const { locale, area } = await params;
  setRequestLocale(locale);
  const { initial, areas } = await loadSearch(await searchParams, area);
  if (areas.length && !areas.some(a => a.slug === area)) notFound();
  return <SearchView initial={initial} areas={areas} area={area} />;
}

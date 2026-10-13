import type { Metadata } from 'next';
import { getTranslations, setRequestLocale } from 'next-intl/server';
import { loadSearch } from './load';
import { SearchView } from './search-view';

// Live data on every request (listings change, and each search is recorded); the CDN caches for 60 s.
export const dynamic = 'force-dynamic';

type Params = { params: Promise<{ locale: string }>; searchParams: Promise<Record<string, string | string[] | undefined>> };

export async function generateMetadata({ params }: Params): Promise<Metadata> {
  const { locale } = await params;
  const t = await getTranslations({ locale });
  return { title: t('nav_find') };
}

export default async function Search({ params, searchParams }: Params) {
  const { locale } = await params;
  setRequestLocale(locale);
  const { initial, areas } = await loadSearch(await searchParams);
  return <SearchView initial={initial} areas={areas} />;
}

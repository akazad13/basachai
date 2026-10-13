import type { Metadata } from 'next';
import { getTranslations, setRequestLocale } from 'next-intl/server';
import { filtersOf, loadSearch } from './load';
import { SearchView } from './search-view';

// Any combination of filters: rendered fresh for each request. Area pages are the cached, indexable entry points.
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
  const filters = filtersOf(await searchParams);
  const { initial, areas } = await loadSearch(filters);
  return <SearchView initial={initial} areas={areas} filters={filters} />;
}

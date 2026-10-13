import { setRequestLocale } from 'next-intl/server';
import type { Lang } from '@/lib/format';
import { ListingPage, listingMetadata } from '../listing-page';

// Served from the edge for 60 seconds, and purged by the API when the listing changes.
export const revalidate = 60;
export function generateStaticParams() { return []; }   // rendered on first request, then cached

type Params = { params: Promise<{ locale: string; shortId: string }> };

export async function generateMetadata({ params }: Params) {
  const { locale, shortId } = await params;
  return listingMetadata(locale as Lang, shortId);
}

export default async function Page({ params }: Params) {
  const { locale, shortId } = await params;
  setRequestLocale(locale);
  return <ListingPage lang={locale as Lang} shortId={shortId} />;
}

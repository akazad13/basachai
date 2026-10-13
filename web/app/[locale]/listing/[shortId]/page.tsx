import { setRequestLocale } from 'next-intl/server';
import type { Lang } from '@/lib/format';
import { ListingPage, listingMetadata } from '../listing-page';

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

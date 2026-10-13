import { setRequestLocale } from 'next-intl/server';
import type { Lang } from '@/lib/format';
import { ListingPage, listingMetadata } from '../../listing-page';

/**
 * The listing as its owner or a moderator sees it, including when it is not live (in review, paused, hidden...).
 * Uses the visitor's session, so it is rendered fresh every time and never indexed. Admin pages link here.
 */
export const dynamic = 'force-dynamic';

type Params = { params: Promise<{ locale: string; shortId: string }> };

export async function generateMetadata({ params }: Params) {
  const { locale, shortId } = await params;
  return listingMetadata(locale as Lang, shortId, true);
}

export default async function Page({ params }: Params) {
  const { locale, shortId } = await params;
  setRequestLocale(locale);
  return <ListingPage lang={locale as Lang} shortId={shortId} preview />;
}

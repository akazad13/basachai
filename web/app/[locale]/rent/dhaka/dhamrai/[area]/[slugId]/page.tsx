import { permanentRedirect } from 'next/navigation';
import { setRequestLocale } from 'next-intl/server';
import type { Lang } from '@/lib/format';
import { ListingPage, canonicalPath, listingMetadata, loadListing } from '@/app/[locale]/listing/listing-page';

/** The readable, canonical property URL: /bn/rent/dhaka/dhamrai/{area}/{slug}-{shortId}. The short id decides. */
// Served from the edge for 60 seconds, and purged by the API when the listing changes.
export const revalidate = 60;
export function generateStaticParams() { return []; }   // rendered on first request, then cached

type Params = { params: Promise<{ locale: string; area: string; slugId: string }> };

const shortIdOf = (slugId: string) => slugId.slice(slugId.lastIndexOf('-') + 1);

export async function generateMetadata({ params }: Params) {
  const { locale, slugId } = await params;
  return listingMetadata(locale as Lang, shortIdOf(slugId));
}

export default async function Page({ params }: Params) {
  const { locale, area, slugId } = await params;
  setRequestLocale(locale);
  const shortId = shortIdOf(slugId);
  // An old slug or area (the title or area changed) moves to the current address, so only one copy is cached and indexed.
  const d = await loadListing(shortId);
  if (d.card && `/${locale}/rent/dhaka/dhamrai/${area}/${slugId}` !== canonicalPath(locale, d)) permanentRedirect(canonicalPath(locale, d));
  return <ListingPage lang={locale as Lang} shortId={shortId} />;
}

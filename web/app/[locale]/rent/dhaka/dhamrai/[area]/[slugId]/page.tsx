import { setRequestLocale } from 'next-intl/server';
import type { Lang } from '@/lib/format';
import { ListingPage, listingMetadata } from '@/app/[locale]/listing/listing-page';

/** The readable, canonical property URL: /bn/rent/dhaka/dhamrai/{area}/{slug}-{shortId}. The short id decides. */
type Params = { params: Promise<{ locale: string; area: string; slugId: string }> };

const shortIdOf = (slugId: string) => slugId.slice(slugId.lastIndexOf('-') + 1);

export async function generateMetadata({ params }: Params) {
  const { locale, slugId } = await params;
  return listingMetadata(locale as Lang, shortIdOf(slugId));
}

export default async function Page({ params }: Params) {
  const { locale, slugId } = await params;
  setRequestLocale(locale);
  return <ListingPage lang={locale as Lang} shortId={shortIdOf(slugId)} />;
}

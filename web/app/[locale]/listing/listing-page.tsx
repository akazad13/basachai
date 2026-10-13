import type { Metadata } from 'next';
import { cache } from 'react';
import { cookies } from 'next/headers';
import { notFound } from 'next/navigation';
import { getTranslations } from 'next-intl/server';
import { Link } from '@/i18n/navigation';
import { ApiError, PAGE_SECONDS, serverGet, TAGS } from '@/lib/api';
import { dayMonth, isAvailableNow, num, type Lang } from '@/lib/format';
import { hueFor, photoUrl } from '@/lib/photos';
import type { ListingDetail } from '@/lib/types';
import { CostBreakdown } from '@/components/cost-breakdown';
import { Freshness } from '@/components/freshness';
import { StatusBadge } from '@/components/status-badge';
import { areaName } from '@/components/listing-card';
import { BackLink, ContactBox, ViewTracker } from './listing-client';
import { Gallery } from './gallery';

const SITE = process.env.NEXT_PUBLIC_SITE_URL ?? 'http://localhost:3000';

/**
 * Loads a listing. The public page is the visitor's view, cached at the edge and purged by the API when the listing
 * changes. A preview (/listing/{id}/preview) forwards the session cookie, so an owner or moderator sees a listing that
 * is not live; it is never cached.
 */
export const loadListing = cache(async (shortId: string, preview = false): Promise<ListingDetail> => {
  if (!isShortId(shortId)) notFound();   // junk ids never reach the API or the page cache
  const path = `/v1/listings/${encodeURIComponent(shortId)}`;
  try {
    if (!preview) return await serverGet<ListingDetail>(path, { revalidate: PAGE_SECONDS, tags: [TAGS.listing(shortId)] });
    const auth = (await cookies()).get('bc_auth');
    return await serverGet<ListingDetail>(path, { headers: auth ? { Cookie: `bc_auth=${auth.value}` } : {} });
  } catch (e) {
    if (e instanceof ApiError && e.status === 404) notFound();
    throw e;
  }
});

export const isShortId = (id: string) => /^[A-Za-z0-9]{1,16}$/.test(id);

/** The canonical, readable URL: /bn/rent/dhaka/dhamrai/{area}/{slug}-{shortId}. */
export function canonicalPath(lang: string, d: ListingDetail) {
  return `/${lang}/rent/dhaka/dhamrai/${d.card!.area.slug}/${d.card!.slug}-${d.card!.shortId}`;
}

export async function listingMetadata(lang: Lang, shortId: string, preview = false): Promise<Metadata> {
  const d = await loadListing(shortId, preview);
  const t = await getTranslations({ locale: lang });
  if (!d.available || !d.card) return { title: d.card?.title ?? t('not_found'), robots: { index: false, follow: false } };
  if (preview) return { title: d.card.title, robots: { index: false, follow: false } };
  const c = d.card;
  const summary = `${areaName(lang, c.area)} · ${c.bedrooms === 0 ? t('studio') : t('beds_n', { n: num(lang, c.bedrooms) })} · ${t('total_month')} ৳${num(lang, c.monthlyTotal)}`;
  return {
    title: c.title,
    description: (d.description ?? summary).slice(0, 155),
    alternates: {
      canonical: SITE + canonicalPath(lang, d),
      languages: { bn: SITE + canonicalPath('bn', d), en: SITE + canonicalPath('en', d) },
    },
    openGraph: { title: c.title, images: c.cover ? [photoUrl(c.cover, 'full')] : [] },
  };
}

export async function ListingPage({ lang, shortId, preview = false }: { lang: Lang; shortId: string; preview?: boolean }) {
  const t = await getTranslations();
  const d = await loadListing(shortId, preview);

  if (!d.card) {
    return (
      <div className="empty" style={{ margin: '40px 0' }}>
        <h1 style={{ fontSize: 26 }}>{t('not_found')}</h1>
        <div className="actions"><Link className="btn btn-primary" href="/">{t('go_home')}</Link></div>
      </div>
    );
  }

  const c = d.card;
  const facts: [string, string][] = [
    ['k_type', t(c.type === 'house' ? 'type_house' : 'type_flat')],
    ['k_beds', c.bedrooms === 0 ? t('studio') : t('beds_n', { n: num(lang, c.bedrooms) })],
    ...(c.bathrooms != null ? [['k_baths', num(lang, c.bathrooms)] as [string, string]] : []),
    ...(c.floorAreaSqft != null ? [['k_size', t('sqft_n', { n: num(lang, c.floorAreaSqft) })] as [string, string]] : []),
    ...(d.floorNumber != null ? [['k_floor', d.floorNumber === 0 ? t('ground') : t('floor_n', { n: num(lang, d.floorNumber) })] as [string, string]] : []),
    ...(d.furnishing ? [['k_furnish', t(`furn_${d.furnishing}` as 'furn_semi')] as [string, string]] : []),
    ...(d.occupancy ? [['k_occ', t(`occ_${d.occupancy}` as 'occ_any')] as [string, string]] : []),
    ['k_avail', isAvailableNow(c.availableFrom) ? t('avail_now') : dayMonth(lang, c.availableFrom!)],
  ];

  return (
    <>
      <p className="crumb"><BackLink label={t('back')} /></p>
      {!d.available && (
        <p className="banner" role="note">
          <StatusBadge status={d.status} /> {d.isHidden ? t('hidden_banner') : t('preview_banner', { s: t(`st_${d.status}`) })}
        </p>
      )}
      {d.available && <ViewTracker shortId={c.shortId} />}
      <div className="detail">
        <Gallery photos={d.photos} hue={hueFor(c.shortId)} />
        <div className="head">
          <Freshness at={c.lastConfirmedAt} />
          <h1 style={{ fontSize: 28, marginTop: 6 }}>{c.title}</h1>
          <div className="meta">
            <span>{areaName(lang, c.area)}</span>
            <span>{c.bedrooms === 0 ? t('studio') : t('beds_n', { n: num(lang, c.bedrooms) })}</span>
            {c.bathrooms != null && <span>{t('baths_n', { n: num(lang, c.bathrooms) })}</span>}
            <span>{isAvailableNow(c.availableFrom) ? t('avail_now') : t('avail_from', { d: dayMonth(lang, c.availableFrom!) })}</span>
          </div>
        </div>
        <aside className="side">
          <CostBreakdown card={c} oneTime={d.oneTimeCosts} />
          <ContactBox shortId={c.shortId} ownerName={d.owner?.displayName ?? ''} live={d.available} />
        </aside>
        <div className="body">
          <section aria-labelledby="about-t">
            <h2 id="about-t">{t('about_t')}</h2>
            <dl className="facts-grid">
              {facts.map(([k, v]) => <div key={k}><dt>{t(k as 'k_type')}</dt><dd>{v}</dd></div>)}
            </dl>
          </section>
          {d.description && <section aria-labelledby="desc-t"><h2 id="desc-t">{t('desc_t')}</h2><p>{d.description}</p></section>}
          {d.amenities.length > 0 && (
            <section aria-labelledby="amen-t">
              <h2 id="amen-t">{t('amen_t')}</h2>
              <ul className="amen">{d.amenities.map(a => <li key={a.key}>{lang === 'bn' ? a.nameBn : a.nameEn}</li>)}</ul>
            </section>
          )}
          {d.rules && <section aria-labelledby="rules-t"><h2 id="rules-t">{t('rules_t')}</h2><p>{d.rules}</p></section>}
          <section aria-labelledby="loc-t">
            <h2 id="loc-t">{t('loc_t')}</h2>
            <p>{areaName(lang, c.area)}, {lang === 'bn' ? 'ধামরাই, ঢাকা' : 'Dhamrai, Dhaka'}</p>
            <p className="hint">{t('loc_d')}</p>
          </section>
        </div>
      </div>
    </>
  );
}

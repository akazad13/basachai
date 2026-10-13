import { getTranslations, setRequestLocale } from 'next-intl/server';
import { Link } from '@/i18n/navigation';
import { PAGE_SECONDS, serverGet, TAGS } from '@/lib/api';
import type { Card, Locations, OneTimeLine, SearchPage } from '@/lib/types';
import { CostBreakdown } from '@/components/cost-breakdown';
import { EyeIcon } from '@/components/icons';
import { ListingCard } from '@/components/listing-card';
import { HomeSearch } from './home-search';

// Served from the edge for 60 seconds, and purged by the API when a listing or setting it shows changes.
export const revalidate = 60;

async function load() {
  const cached = (tags: string[]) => ({ revalidate: PAGE_SECONDS, tags });
  const [recent, home, locations] = await Promise.allSettled([
    serverGet<SearchPage>('/v1/listings?sort=confirmed&size=4', { headers: { 'X-Search-Source': 'none' }, ...cached([TAGS.listings]) }),   // not a search
    serverGet<{ costExample: { card: Card; oneTimeCosts: OneTimeLine[] } | null }>('/v1/home', cached([TAGS.listings, TAGS.reference])),
    serverGet<Locations>('/v1/locations', cached([TAGS.reference])),
  ]);
  // A cached page must not store an outage: throwing keeps the last good page (or shows the error page uncached).
  for (const r of [recent, home, locations]) if (r.status === 'rejected') throw r.reason;
  return {
    recent: recent.status === 'fulfilled' ? recent.value.items : null,
    example: home.status === 'fulfilled' ? home.value.costExample : null,
    areas: locations.status === 'fulfilled' ? locations.value.areas : [],
  };
}

/** Never leave one card alone on a row of the 2 x 2 grid: with 3 homes, show 2 (home spec). */
function forGrid(cards: Card[]) {
  return cards.length === 3 ? cards.slice(0, 2) : cards.slice(0, 4);
}

export default async function HomePage({ params }: { params: Promise<{ locale: string }> }) {
  const { locale } = await params;
  setRequestLocale(locale);
  const t = await getTranslations();
  const { recent, example, areas } = await load();

  return (
    <>
      <section className="hero">
        <div>
          <h1>{t('hero_title')}</h1>
          <p className="sub">{t('hero_sub')}</p>
          <HomeSearch areas={areas} />
        </div>
        {example && (
          <div className="hero-aside">
            <span className="badge b-brass" style={{ marginBottom: 8 }}>{t('example')}</span>
            <CostBreakdown card={example.card} oneTime={example.oneTimeCosts} />
            <p className="cap">{t('hero_cap')}</p>
          </div>
        )}
      </section>

      {recent === null ? (
        <section className="section" aria-labelledby="recent-t">
          <h2 id="recent-t">{t('recent_t')}</h2>
          <div className="empty"><p>{t('load_error')}</p><div className="actions"><Link className="btn btn-primary btn-sm" href="/">{t('retry')}</Link></div></div>
        </section>
      ) : recent.length > 0 && (
        <section className="section" aria-labelledby="recent-t">
          <div className="section-head"><h2 id="recent-t">{t('recent_t')}</h2><Link href="/search">{t('see_all')}</Link></div>
          <div className="cards grid4" data-testid="recent-grid">
            {forGrid(recent).map((c, i) => <ListingCard key={c.shortId} card={c} priority={i < 2} referrer="home" />)}
          </div>
        </section>
      )}

      <section className="section" aria-labelledby="how-t">
        <h2 id="how-t" style={{ marginBottom: 16 }}>{t('how_t')}</h2>
        <ol className="steps">
          {(['1', '2', '3'] as const).map(n => (
            <li key={n}><h3>{t(`how${n}_t`)}</h3><p>{t(`how${n}_d`)}</p></li>
          ))}
        </ol>
      </section>

      <section className="section" aria-labelledby="safety-t">
        <div className="safety"><EyeIcon /><div><h3 id="safety-t">{t('safety_t')}</h3><p>{t('safety_d')}</p></div></div>
      </section>
    </>
  );
}

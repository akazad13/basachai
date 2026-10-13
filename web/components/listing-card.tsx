import { useLocale, useTranslations } from 'next-intl';
import { Link } from '@/i18n/navigation';
import { dayMonth, isAvailableNow, money, num, type Lang } from '@/lib/format';
import { hueFor } from '@/lib/photos';
import type { Card } from '@/lib/types';
import { Freshness } from './freshness';
import { PhotoTile } from './photo-tile';
import { SaveButton } from './save-button';

export function areaName(lang: Lang, area: { nameBn: string; nameEn: string }) {
  return lang === 'bn' ? area.nameBn : area.nameEn;
}

/** A listing card for search, home and saved homes. With `preview` (the posting review step) it has no links and no Save button. */
export function ListingCard({ card, priority, referrer, preview }: { card: Card; priority?: boolean; referrer?: 'search' | 'home' | 'saved'; preview?: boolean }) {
  const t = useTranslations();
  const lang = useLocale() as Lang;
  const href = { pathname: `/listing/${card.shortId}`, query: referrer ? { from: referrer } : undefined };
  const extra = card.monthlyOthers.filter(o => o.amount != null).length;
  const split = [
    `${t('rent')} ${money(lang, card.rent)}`,
    card.serviceCharge ? `${t('svc_short')} ${money(lang, card.serviceCharge)}` : null,
    extra ? t('split_more', { n: num(lang, extra) }) : null,
  ].filter(Boolean).join(' + ');
  return (
    <article className="card">
      {preview ? (
        <div className="card-photo"><PhotoTile photo={card.cover} hue={hueFor(card.shortId)} size="card" index={0} count={card.photoCount} priority /></div>
      ) : (
        <Link className="card-photo" href={href} tabIndex={-1} aria-hidden="true">
          <PhotoTile photo={card.cover} hue={hueFor(card.shortId)} size="card" index={0} count={card.photoCount} priority={priority} />
        </Link>
      )}
      <div className="card-body">
        <div className="card-top">
          <Freshness at={card.lastConfirmedAt} />
          {!preview && <SaveButton shortId={card.shortId} />}
        </div>
        <h3>{preview ? card.title : <Link href={href}>{card.title}</Link>}</h3>
        <p className="area">{areaName(lang, card.area)}, {t(card.type === 'house' ? 'type_house' : 'type_flat')}</p>
        <ul className="facts">
          <li>{card.bedrooms === 0 ? t('studio') : t('beds_n', { n: num(lang, card.bedrooms) })}</li>
          {card.bathrooms != null && <li>{t('baths_n', { n: num(lang, card.bathrooms) })}</li>}
          {card.floorAreaSqft != null && <li>{t('sqft_n', { n: num(lang, card.floorAreaSqft) })}</li>}
          <li>{isAvailableNow(card.availableFrom) ? t('avail_now') : t('avail_from', { d: dayMonth(lang, card.availableFrom!) })}</li>
        </ul>
        <div className="card-cost">
          <div className="row"><span className="lbl">{t('total_month')}</span><span className="amt">{money(lang, card.monthlyTotal)}</span></div>
          <div className="split">{split}</div>
          <div className="split">{t('advance')}: {card.advance.amount != null ? money(lang, card.advance.amount) : t('not_specified')}</div>
        </div>
      </div>
    </article>
  );
}

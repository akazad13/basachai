import { useLocale, useTranslations } from 'next-intl';
import { money, type Lang } from '@/lib/format';
import type { Card, OneTimeLine } from '@/lib/types';

/**
 * The signature element: each cost on its own line, a brass double rule, the true monthly total,
 * and one-time costs in a separate box that never adds to the total (specs/domain/listing-and-pricing.md).
 */
export function CostBreakdown({ card, oneTime, title = true }: { card: Card; oneTime: OneTimeLine[]; title?: boolean }) {
  const t = useTranslations();
  const lang = useLocale() as Lang;
  const unknown = card.monthlyOthers.filter(o => o.amount == null).map(o => o.label);
  const row = (label: string, value: string, na: boolean, key: string) => (
    <div className="r-row" key={key}>
      <span>{label}</span><span className="dots" aria-hidden="true" /><span className={`amt${na ? ' na' : ''}`}>{value}</span>
    </div>
  );
  return (
    <div className="receipt" data-testid="cost-breakdown">
      {title && <h3>{t('costs_t')}</h3>}
      {row(t('rent'), money(lang, card.rent), false, 'rent')}
      {row(t('service'), card.serviceCharge ? money(lang, card.serviceCharge) : t('none'), !card.serviceCharge, 'service')}
      {card.monthlyOthers.map((o, i) => row(o.label, o.amount != null ? money(lang, o.amount) : t('meter'), o.amount == null, `m${i}`))}
      <div className="r-total">
        <span className="lbl">{t('total_month')}</span>
        <span className="amt" data-testid="cost-total">{money(lang, card.monthlyTotal)}</span>
      </div>
      {unknown.length > 0 && <p className="r-note">{t('excl_note', { x: unknown.join(', ') })}</p>}
      <div className="r-once" role="group" aria-label={t('one_time_head')}>
        <div className="r-head">{t('one_time_head')}</div>
        {oneTime.map((o, i) => row(o.isAdvance ? t('advance') : o.label,
          o.amount != null ? money(lang, o.amount) : t('not_specified'), o.amount == null, `o${i}`))}
      </div>
    </div>
  );
}

/** The one-time lines a card can show without a detail call: just the advance. */
export function advanceLine(card: Card): OneTimeLine[] {
  return [{ label: 'advance', amount: card.advance.amount, isAdvance: true }];
}

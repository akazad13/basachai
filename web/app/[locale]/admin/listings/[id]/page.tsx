'use client';

import { use, useEffect, useState } from 'react';
import { useLocale, useTranslations } from 'next-intl';
import { api } from '@/lib/api';
import { money, num, type Lang } from '@/lib/format';
import type { HistoryItem } from '@/lib/types';

const MONEY = new Set(['monthlyRent', 'serviceCharge']);

/** Status changes, moderator actions and edits made while Live, oldest first: the check on D23. */
export default function HistoryPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const t = useTranslations();
  const lang = useLocale() as Lang;
  const [items, setItems] = useState<HistoryItem[] | null>(null);
  useEffect(() => { void api<HistoryItem[]>(`/v1/admin/listings/${id}/history`).then(setItems); }, [id]);

  const when = (iso: string) => new Intl.DateTimeFormat(lang === 'bn' ? 'bn-BD' : 'en-GB', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'Asia/Dhaka' }).format(new Date(iso));
  const status = (s: string) => (t.has(`st_${s}` as 'st_draft') ? t(`st_${s}` as 'st_draft') : s);
  const actor = (a: string | null) => (a && t.has(`actor_${a}` as 'actor_owner') ? t(`actor_${a}` as 'actor_owner') : '');
  const value = (key: string, v: unknown) =>
    v == null ? '–'
      : typeof v === 'number' ? (MONEY.has(key) ? money(lang, v) : num(lang, v))
      : typeof v === 'object' ? JSON.stringify(v) : String(v);

  return (
    <>
      <div className="dash-head"><h1 style={{ fontSize: 28 }}>{t('history_t')}</h1></div>
      <ol style={{ paddingLeft: 20 }}>
        {items?.map((h, i) => (
          <li key={i} style={{ marginBottom: 14 }}>
            <strong>
              {h.kind === 'edit' ? t('history_edit') : h.kind === 'status'
                ? `${t('h_status')}: ${h.action.split('->').map(status).join(' → ')}`
                : `${t('h_moderation')}: ${h.action}`}
            </strong>{' '}
            <span className="hint">{when(h.at)} · {actor(h.actorType)}</span>
            {h.reason && <div className="reason">{h.reason}</div>}
            {h.kind === 'edit' && h.after && (
              <div className="diff">
                <span className="hint" /><span className="hint">{t('before')}</span><span className="hint">{t('after')}</span>
                {Object.keys(h.after).map(k => (
                  <span key={k} style={{ display: 'contents' }}>
                    <span>{k}</span><span>{value(k, h.before?.[k])}</span><span>{value(k, h.after?.[k])}</span>
                  </span>
                ))}
              </div>
            )}
          </li>
        ))}
      </ol>
    </>
  );
}

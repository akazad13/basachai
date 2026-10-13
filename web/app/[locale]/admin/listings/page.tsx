'use client';

import { Suspense, useCallback, useEffect, useState, type FormEvent } from 'react';
import { useSearchParams } from 'next/navigation';
import { useLocale, useTranslations } from 'next-intl';
import { Link, usePathname, useRouter } from '@/i18n/navigation';
import { api } from '@/lib/api';
import { money, type Lang } from '@/lib/format';
import type { ListingStatus, QueueItem } from '@/lib/types';
import { areaName } from '@/components/listing-card';
import { StatusBadge } from '@/components/status-badge';
import { useToast } from '@/components/toast';

const STATUSES: ListingStatus[] = ['pending', 'published', 'changes', 'rejected', 'paused', 'rented', 'expired', 'draft', 'archived'];

export default function AdminListingsPage() {
  return <Suspense><AdminListings /></Suspense>;
}

/** Find any listing by title, status or short id; open its history; hide or unhide it. */
function AdminListings() {
  const t = useTranslations();
  const lang = useLocale() as Lang;
  const toast = useToast();
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const q = params.get('q') ?? '';
  const status = params.get('status') ?? '';
  const [rows, setRows] = useState<QueueItem[] | null>(null);

  const load = useCallback(async () => {
    const qs = new URLSearchParams();
    if (q) qs.set('q', q);
    if (status) qs.set('status', status);
    setRows(await api<QueueItem[]>(`/v1/admin/listings?${qs}`));
  }, [q, status]);
  useEffect(() => { void load().catch(() => toast(t('error_generic'))); }, [load, toast, t]);

  function search(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    const qs = new URLSearchParams();
    for (const k of ['q', 'status']) { const v = String(f.get(k) ?? '').trim(); if (v) qs.set(k, v); }
    router.replace(`${pathname}${qs.size ? '?' + qs : ''}`);
  }

  async function toggleHidden(row: QueueItem) {
    try {
      await api(`/v1/admin/listings/${row.id}/${row.isHidden ? 'unhide' : 'hide'}`, { method: 'POST' });
      toast(t(row.isHidden ? 't_unhidden' : 't_hidden'));
      await load();
    } catch { toast(t('error_generic')); }
  }

  return (
    <>
      <div className="dash-head"><h1 style={{ fontSize: 28 }}>{t('listings_t')}</h1></div>
      <form className="search-card" onSubmit={search} style={{ marginBottom: 16 }}>
        <div className="field">
          <label className="f" htmlFor="al-q">{t('search_listings')}</label>
          <input id="al-q" name="q" defaultValue={q} key={q} />
        </div>
        <div className="field">
          <label className="f" htmlFor="al-status">{t('status_t')}</label>
          <select id="al-status" name="status" defaultValue={status} key={status}>
            <option value="">{t('status_any')}</option>
            {STATUSES.map(s => <option key={s} value={s}>{t(`st_${s}`)}</option>)}
          </select>
        </div>
        <button className="btn btn-primary" type="submit">{t('search_btn')}</button>
      </form>
      <div className="table-wrap">
        <table className="data">
          <thead><tr><th>{t('l_title')}</th><th>{t('f_area')}</th><th>{t('advertiser')}</th><th>{t('total_month')}</th><th /></tr></thead>
          <tbody>
            {rows?.map(r => (
              <tr key={r.id}>
                <td>
                  <StatusBadge status={r.status} /> {r.isHidden && <span className="badge b-red">{t('hidden_banner')}</span>}
                  <div><Link href={`/listing/${r.shortId}`}>{r.card.title}</Link> <span className="hint">{r.shortId}</span></div>
                </td>
                <td>{areaName(lang, r.card.area)}</td>
                <td>{r.ownerName}</td>
                <td>{money(lang, r.card.monthlyTotal)}</td>
                <td style={{ whiteSpace: 'nowrap' }}>
                  <Link href={`/admin/listings/${r.id}`}>{t('history')}</Link>{' '}
                  {(r.status === 'published' || r.isHidden) && (
                    <button type="button" className="btn btn-ghost btn-sm" onClick={() => toggleHidden(r)}>{t(r.isHidden ? 'unhide' : 'hide_listing')}</button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

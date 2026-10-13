'use client';

import { useEffect, useState, type FormEvent } from 'react';
import { useLocale, useTranslations } from 'next-intl';
import { api } from '@/lib/api';
import type { Lang } from '@/lib/format';

type Entry = { id: string; actorId: string | null; action: string; entityType: string; entityId: string; createdAt: string; before: unknown; after: unknown };

/** Admin only: who did what, filterable by action, entity and date. Append-only on the server. */
export default function AuditPage() {
  const t = useTranslations();
  const lang = useLocale() as Lang;
  const [rows, setRows] = useState<Entry[] | null>(null);
  const [filter, setFilter] = useState('');

  useEffect(() => { void api<Entry[]>(`/v1/admin/audit${filter ? '?' + filter : ''}`).then(setRows); }, [filter]);

  function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    const qs = new URLSearchParams();
    for (const k of ['action', 'entity', 'from']) { const v = String(f.get(k) ?? '').trim(); if (v) qs.set(k, k === 'from' ? new Date(v).toISOString() : v); }
    setFilter(qs.toString());
  }

  const when = (iso: string) => new Intl.DateTimeFormat(lang === 'bn' ? 'bn-BD' : 'en-GB', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'Asia/Dhaka' }).format(new Date(iso));
  return (
    <>
      <div className="dash-head"><h1 style={{ fontSize: 28 }}>{t('audit_t')}</h1></div>
      <form className="search-card" onSubmit={submit} style={{ marginBottom: 16 }}>
        <div className="field"><label className="f" htmlFor="au-action">{t('audit_action')}</label><input id="au-action" name="action" placeholder="listing." /></div>
        <div className="field"><label className="f" htmlFor="au-entity">{t('audit_entity')}</label><input id="au-entity" name="entity" placeholder="listing" /></div>
        <div className="field"><label className="f" htmlFor="au-from">{t('audit_from')}</label><input id="au-from" name="from" type="date" /></div>
        <button className="btn btn-primary" type="submit">{t('search_btn')}</button>
      </form>
      <div className="table-wrap">
        <table className="data">
          <thead><tr><th>{t('audit_when')}</th><th>{t('audit_action')}</th><th>{t('audit_entity')}</th><th>{t('audit_details')}</th></tr></thead>
          <tbody>
            {rows?.map(r => (
              <tr key={r.id}>
                <td style={{ whiteSpace: 'nowrap' }}>{when(r.createdAt)}</td>
                <td><code>{r.action}</code></td>
                <td>{r.entityType} <span className="hint">{r.entityId.slice(0, 8)}</span></td>
                <td><code style={{ fontSize: 12 }}>{r.after ? JSON.stringify(r.after) : ''}</code></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

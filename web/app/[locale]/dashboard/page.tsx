'use client';

import { useCallback, useEffect, useState } from 'react';
import { useLocale, useTranslations } from 'next-intl';
import { Link, useRouter } from '@/i18n/navigation';
import { api, ApiError } from '@/lib/api';
import { money, num, type Lang } from '@/lib/format';
import { hueFor } from '@/lib/photos';
import type { ListingStatus, OwnerListing } from '@/lib/types';
import { PhotoTile } from '@/components/photo-tile';
import { useSession } from '@/components/session';
import { StatusBadge } from '@/components/status-badge';
import { useToast } from '@/components/toast';

type Action = 'reconfirm' | 'renew' | 'pause' | 'resume' | 'mark-rented' | 'archive';

const ACTIONS: Partial<Record<ListingStatus, (Action | 'edit' | 'continue')[]>> = {
  draft: ['continue', 'archive'],
  published: ['reconfirm', 'pause', 'mark-rented', 'edit'],
  paused: ['resume', 'mark-rented'],
  expired: ['renew', 'archive'],
  changes: ['edit', 'archive'],
  rented: ['archive'],
  rejected: ['archive'],
};
const LABEL: Record<Action | 'edit' | 'continue', string> = {
  reconfirm: 'reconfirm', renew: 'renew', pause: 'pause', resume: 'resume', 'mark-rented': 'mark_rented',
  archive: 'archive', edit: 'edit', continue: 'continue',
};
const TOAST: Record<Action, string> = {
  reconfirm: 't_reconfirmed', renew: 't_renewed', pause: 't_paused', resume: 't_resumed', 'mark-rented': 't_rented', archive: 't_archived',
};
const PRIMARY = new Set(['renew', 'continue']);

/** Everything an owner has, what needs attention, and the actions each status allows (owner-dashboard spec). */
export default function DashboardPage() {
  const t = useTranslations();
  const lang = useLocale() as Lang;
  const toast = useToast();
  const router = useRouter();
  const { me, ready, openSignIn, refreshAttention } = useSession();
  const [rows, setRows] = useState<OwnerListing[] | null>(null);
  const [failed, setFailed] = useState(false);
  const [busy, setBusy] = useState<string | null>(null);

  const load = useCallback(async () => {
    try { setRows(await api<OwnerListing[]>('/v1/owner/listings')); setFailed(false); }
    catch { setFailed(true); }
  }, []);

  useEffect(() => {
    if (!ready) return;
    if (!me) { openSignIn({ reason: 'signin_why_post' }); return; }
    void load();
  }, [ready, me, openSignIn, load]);

  async function act(row: OwnerListing, action: Action | 'edit' | 'continue') {
    if (action === 'edit' || action === 'continue') { router.push({ pathname: '/post', query: { draft: row.id } }); return; }
    setBusy(row.id + action);
    try {
      const updated = await api<OwnerListing>(`/v1/owner/listings/${row.id}/${action}`, { method: 'POST' });
      setRows(prev => prev && (action === 'archive' ? prev.filter(r => r.id !== row.id) : prev.map(r => (r.id === row.id ? updated : r))));
      toast(t(TOAST[action] as 't_paused'));
      await refreshAttention();
    } catch (e) {
      toast(e instanceof ApiError && t.has(e.code as 'restricted') ? t(e.code as 'restricted') : t('error_generic'));
      await load();
    } finally {
      setBusy(null);
    }
  }

  if (!ready || !me) return <div style={{ padding: '40px 0' }} />;

  type Banner = { r: OwnerListing; tone: 'red' | 'brass'; order: number; text: string; action: 'edit' | 'renew' | 'reconfirm' };
  const banners = (rows ?? []).flatMap((r): Banner[] => {
    if (r.status === 'changes') return [{ r, tone: 'red', order: 0, text: t('al_changes', { x: r.title }), action: 'edit' }];
    if (r.status === 'expired') return [{ r, tone: 'red', order: 1, text: t('al_expired', { x: r.title }), action: 'renew' }];
    if (r.status === 'published' && r.daysLeft != null && r.daysLeft <= 3)
      return [{ r, tone: 'brass', order: 2, text: t('al_expiring', { x: r.title, n: num(lang, r.daysLeft) }), action: 'reconfirm' }];
    return [];
  }).sort((a, b) => a.order - b.order);

  return (
    <div className="dash">
      <div className="dash-head">
        <h1 style={{ fontSize: 28 }}>{t('dash_t')}</h1>
        <Link className="btn btn-primary" href="/post">{t('new_listing')}</Link>
      </div>

      {failed && <div className="empty" role="alert"><p>{t('load_error')}</p><button className="btn btn-primary btn-sm" onClick={load}>{t('retry')}</button></div>}

      {banners.length > 0 && (
        <div className="alerts" data-testid="attention-banners">
          {banners.map(b => (
            <div className={`alert ${b.tone}`} key={b.r.id + b.action}>
              <p>{b.text}</p>
              <div className="acts">
                <button type="button" className={`btn btn-sm ${b.action === 'edit' ? 'btn-ghost' : 'btn-primary'}`} disabled={busy !== null}
                  onClick={() => act(b.r, b.action)}>{t(LABEL[b.action] as 'edit')}</button>
              </div>
            </div>
          ))}
        </div>
      )}

      {rows && rows.length === 0 && (
        <div className="empty"><p>{t('no_listings')}</p><div className="actions"><Link className="btn btn-primary" href="/post">{t('new_listing')}</Link></div></div>
      )}

      {rows?.map(r => {
        const sub: string[] = [];
        if ((r.status === 'published' || r.status === 'paused') && r.daysLeft != null) sub.push(t('days_left', { n: num(lang, r.daysLeft) }));
        if (r.status === 'pending') sub.push(t('mod_review'));
        if (r.views) sub.push(t('views_n', { n: num(lang, r.views) }));
        if (r.reveals) sub.push(t('reveals_n', { n: num(lang, r.reveals) }));
        const actions = ACTIONS[r.status] ?? [];
        return (
          <article className="lrow" key={r.id}>
            <PhotoTile photo={r.cover} hue={hueFor(r.shortId)} size="card" />
            <div>
              <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                <StatusBadge status={r.status} />
                {r.isHidden && <span className="badge b-red">{t('hidden_banner')}</span>}
                <span className="hint">{money(lang, r.monthlyTotal)} / {t('total_month')}</span>
              </div>
              <h3 style={{ marginTop: 4 }}>
                {r.status === 'published' && !r.isHidden ? <Link href={`/listing/${r.shortId}`} style={{ color: 'inherit' }}>{r.title}</Link> : r.title}
              </h3>
              <div className="sub">{sub.map(s => <span key={s}>{s}</span>)}</div>
              {(r.status === 'changes' || r.status === 'rejected') && r.moderatorReason && (
                <div className="reason"><strong>{t('mod_reason')}:</strong> {r.moderatorReason}</div>
              )}
              {actions.length > 0 && (
                <div className="acts">
                  {actions.map(a => (
                    <button key={a} type="button" className={`btn btn-sm ${PRIMARY.has(a) ? 'btn-primary' : 'btn-ghost'}`}
                      disabled={busy !== null} onClick={() => act(r, a)}>{t(LABEL[a] as 'edit')}</button>
                  ))}
                </div>
              )}
            </div>
          </article>
        );
      })}
    </div>
  );
}

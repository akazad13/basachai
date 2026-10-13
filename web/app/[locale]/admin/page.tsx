'use client';

import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react';
import { useLocale, useTranslations } from 'next-intl';
import { Link } from '@/i18n/navigation';
import { api, ApiError } from '@/lib/api';
import { ageText, dataSize, num, percent, type Lang } from '@/lib/format';
import { hueFor } from '@/lib/photos';
import type { PhotoUsage, QueueItem, Report, Stats } from '@/lib/types';
import { CostBreakdown } from '@/components/cost-breakdown';
import { areaName } from '@/components/listing-card';
import { PhotoTile } from '@/components/photo-tile';
import { useSession } from '@/components/session';
import { useToast } from '@/components/toast';

type Decline = { id: string; kind: 'request-changes' | 'reject' };

/** Review queue (oldest first), open reports and summary counts (moderation-and-admin spec). */
export default function ModerationPage() {
  const t = useTranslations();
  const lang = useLocale() as Lang;
  const toast = useToast();
  const [stats, setStats] = useState<Stats | null>(null);
  const [queue, setQueue] = useState<QueueItem[] | null>(null);
  const [reports, setReports] = useState<Report[] | null>(null);
  const [decline, setDecline] = useState<Decline | null>(null);
  const [busy, setBusy] = useState(false);
  const { me } = useSession();
  const isAdmin = !!me?.roles.includes('admin');
  const [usage, setUsage] = useState<PhotoUsage | null>(null);

  // Admins also see the photo service's monthly usage, fetched when the page opens.
  useEffect(() => {
    if (!isAdmin) return;
    api<PhotoUsage>('/v1/admin/photo-usage').then(setUsage, () => setUsage({ available: false, nearLimit: false, usage: null }));
  }, [isAdmin]);

  const load = useCallback(async () => {
    const [s, q, r] = await Promise.all([
      api<Stats>('/v1/admin/stats'), api<QueueItem[]>('/v1/admin/listings?status=pending'), api<Report[]>('/v1/admin/reports'),
    ]);
    setStats(s); setQueue(q); setReports(r);
  }, []);
  useEffect(() => { void load().catch(() => toast(t('error_generic'))); }, [load, toast, t]);

  async function run(call: () => Promise<unknown>, done: string) {
    setBusy(true);
    try { await call(); toast(t(done as 't_approved')); }
    catch (e) { toast(e instanceof ApiError && t.has(e.code as 'already_handled') ? t(e.code as 'already_handled') : t('error_generic')); }
    finally { setBusy(false); await load(); }
  }

  const stat = (label: string, value: number, red = false) => (
    <div role="group" aria-label={label}>
      <div className={`v${red && value > 0 ? ' red' : ''}`}>{num(lang, value)}</div>
      <div className="k">{label}</div>
    </div>
  );
  const metric = (label: string, value: string | null) => (
    <div role="group" aria-label={label}><div className="v" style={{ fontSize: 22 }}>{value ?? '–'}</div><div className="k">{label}</div></div>
  );

  return (
    <>
      <div className="dash-head"><h1 style={{ fontSize: 28 }}>{t('admin_t')}</h1></div>
      {stats && (
        <>
          <div className="stats">
            {stat(t('s_pending'), stats.pending)}
            {stat(t('s_live'), stats.live)}
            {stat(t('s_reports'), stats.openReports)}
            {stat(t('s_overdue'), stats.overdue, true)}
          </div>
          <details style={{ marginBottom: 24 }}>
            <summary>{t('metrics_t')}</summary>
            <div className="stats" style={{ marginTop: 12 }}>
              {metric(t('m_completeness'), percent(lang, stats.metrics.completeness))}
              {metric(t('m_freshness'), percent(lang, stats.metrics.freshness))}
              {metric(t('m_search_contact'), percent(lang, stats.metrics.searchToContact))}
              {metric(t('m_activation'), num(lang, stats.metrics.ownersActivated))}
              {metric(t('m_reports'), percent(lang, stats.metrics.reportHandling))}
            </div>
          </details>
        </>
      )}
      {/* Admins only; shown even if the counts fail to load. */}
      {usage && (
        <section className="panel" aria-labelledby="usage-t" style={{ marginBottom: 24 }}>
          <h2 id="usage-t" style={{ fontSize: 18 }}>{t('usage_t')}</h2>
          {usage.usage ? (
            <>
              <p>{t('usage_credits', { p: percent(lang, usage.usage.usedPercent / 100, 1) ?? '', used: num(lang, usage.usage.creditsUsed), limit: num(lang, usage.usage.creditsLimit) })}</p>
              {usage.nearLimit && <p className="err" role="status">{t('usage_warn')}</p>}
              <div className="stats">
                {metric(t('usage_storage'), dataSize(lang, usage.usage.storageBytes))}
                {metric(t('usage_bandwidth'), dataSize(lang, usage.usage.bandwidthBytes))}
                {metric(t('usage_transforms'), num(lang, usage.usage.transformations))}
              </div>
            </>
          ) : <p className="hint">{t('usage_none')}</p>}
        </section>
      )}
      <div className="admin-cols">
        <section aria-labelledby="queue-t">
          <h2 id="queue-t" style={{ marginBottom: 12 }}>{t('queue_t')}</h2>
          <div data-testid="review-queue">
            {queue?.length === 0 && <div className="empty"><p>{t('queue_empty')}</p></div>}
            {queue?.map(q => (
              <article className="qcard" key={q.id}>
                <div>
                  <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'center' }}>
                    {q.submittedAt && <span className="hint">{t('submitted_ago', { t: ageText(lang, q.submittedAt) })}</span>}
                    {q.overdue && <span className="badge b-red">{t('overdue')}</span>}
                  </div>
                  <h3 style={{ marginTop: 4 }}>{q.card.title}</h3>
                  <p className="hint" style={{ margin: 0, display: 'flex', gap: 12, flexWrap: 'wrap' }}>
                    <span>{areaName(lang, q.card.area)}</span><span>{q.ownerName}</span>
                  </p>
                  <div className="mini-tiles">
                    {q.card.cover && <PhotoTile photo={q.card.cover} hue={hueFor(q.shortId)} size="card" />}
                  </div>
                  <p className="hint" style={{ margin: 0 }}>{t('photos_count', { n: num(lang, q.card.photoCount) })} · <Link href={`/listing/${q.shortId}`}>{t('view_details')}</Link></p>
                  {q.description && <p style={{ marginTop: 8 }}>{q.description}</p>}
                </div>
                <CostBreakdown card={q.card} oneTime={q.oneTimeCosts} title={false} />
                <div className="acts">
                  <button type="button" className="btn btn-primary btn-sm" disabled={busy}
                    onClick={() => run(() => api(`/v1/admin/listings/${q.id}/approve`, { method: 'POST' }), 't_approved')}>{t('approve')}</button>
                  <button type="button" className="btn btn-ghost btn-sm" disabled={busy} onClick={() => setDecline({ id: q.id, kind: 'request-changes' })}>{t('request_changes')}</button>
                  <button type="button" className="btn btn-danger btn-sm" disabled={busy} onClick={() => setDecline({ id: q.id, kind: 'reject' })}>{t('reject')}</button>
                </div>
              </article>
            ))}
          </div>
        </section>
        <section aria-labelledby="reports-t">
          <h2 id="reports-t" style={{ marginBottom: 12 }}>{t('reports_t')}</h2>
          <div data-testid="reports-list">
            {reports?.length === 0 && <div className="empty"><p>{t('reports_empty')}</p></div>}
            {reports?.map(r => (
              <article className="rep" key={r.id}>
                <div className="top">
                  <span className={`badge ${r.reason === 'suspicious' ? 'b-red' : 'b-brass'}`}>{t(`r_${r.reason}`)}</span>
                  <span className="hint">{t('reported_ago', { t: ageText(lang, r.createdAt) })}{r.overdue && <> <span className="badge b-red">{t('overdue')}</span></>}</span>
                </div>
                <h3 style={{ margin: '6px 0 0', fontSize: 16 }}><Link href={`/listing/${r.listingShortId}`}>{r.listingTitle}</Link></h3>
                {r.details && <blockquote>{r.details}</blockquote>}
                <div className="acts">
                  <button type="button" className="btn btn-danger btn-sm" disabled={busy}
                    onClick={() => run(() => api(`/v1/admin/reports/${r.id}/resolve`, { method: 'POST', json: { action: 'hide' } }), 't_hidden')}>{t('hide_listing')}</button>
                  <button type="button" className="btn btn-ghost btn-sm" disabled={busy}
                    onClick={() => run(() => api(`/v1/admin/reports/${r.id}/resolve`, { method: 'POST', json: { action: 'dismiss' } }), 't_dismissed')}>{t('dismiss')}</button>
                </div>
              </article>
            ))}
          </div>
        </section>
      </div>
      {decline && <ReasonDialog decline={decline} onClose={() => setDecline(null)}
        onSent={() => { setDecline(null); toast(t(decline.kind === 'reject' ? 't_rejected' : 't_changes')); void load(); }} />}
    </>
  );
}

function ReasonDialog({ decline, onClose, onSent }: { decline: Decline; onClose: () => void; onSent: () => void }) {
  const t = useTranslations();
  const ref = useRef<HTMLDialogElement>(null);
  const [error, setError] = useState('');
  useEffect(() => { if (ref.current && !ref.current.open) ref.current.showModal(); }, []);

  async function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const reason = String(new FormData(e.currentTarget).get('reason') ?? '').trim();
    if (!reason) { setError(t('reason_err')); return; }
    try {
      await api(`/v1/admin/listings/${decline.id}/${decline.kind}`, { method: 'POST', json: { reason } });
      onSent();
    } catch (err) {
      setError(err instanceof ApiError && t.has(err.code as 'already_handled') ? t(err.code as 'already_handled') : t('error_generic'));
    }
  }

  return (
    <dialog ref={ref} aria-labelledby="reason-t" onClose={onClose}>
      <form className="dlg" onSubmit={submit} noValidate>
        <h2 id="reason-t">{t(decline.kind === 'reject' ? 'reason_t_reject' : 'reason_t_changes')}</h2>
        <label className="f" htmlFor="m-reason">{t('reason_label')}</label>
        <textarea id="m-reason" name="reason" maxLength={500} aria-invalid={error ? 'true' : undefined} />
        {error && <p className="err" role="alert">{error}</p>}
        <div className="acts">
          <button type="button" className="btn btn-ghost" onClick={() => ref.current?.close()}>{t('cancel')}</button>
          <button type="submit" className={`btn ${decline.kind === 'reject' ? 'btn-danger' : 'btn-primary'}`}>{t('send')}</button>
        </div>
      </form>
    </dialog>
  );
}

'use client';

import { useEffect, useRef, useState, type FormEvent } from 'react';
import { useLocale, useTranslations } from 'next-intl';
import { Link } from '@/i18n/navigation';
import { api, ApiError } from '@/lib/api';
import { num, phoneDisplay, type Lang } from '@/lib/format';
import { SaveButton } from '@/components/save-button';
import { useToast } from '@/components/toast';

/** Returns to the last search (remembered by the search page), or to /search. */
export function BackLink({ label }: { label: string }) {
  const [href, setHref] = useState('/search');
  useEffect(() => {
    try { const last = sessionStorage.getItem('bc_last_search'); if (last) setHref(last); } catch { /* storage blocked */ }
  }, []);
  return <Link href={href}>{label}</Link>;
}

/** Records one listing_viewed per page load; the API counts a session once an hour. */
export function ViewTracker({ shortId }: { shortId: string }) {
  useEffect(() => {
    const from = new URLSearchParams(location.search).get('from');
    void api('/v1/events', { method: 'POST', json: { name: 'listing_viewed', listingId: shortId, referrer: from ?? 'direct' } }).catch(() => {});
  }, [shortId]);
  return null;
}

/** Owner name, honest labels, the number on request only (never in the page before), save and report. */
export function ContactBox({ shortId, ownerName, live }: { shortId: string; ownerName: string; live: boolean }) {
  const t = useTranslations();
  const lang = useLocale() as Lang;
  const [phone, setPhone] = useState<string | null>(null);
  const [limited, setLimited] = useState<number | null>(null);
  const [error, setError] = useState(false);
  const [reporting, setReporting] = useState(false);
  const [revealing, setRevealing] = useState(false);
  const callRef = useRef<HTMLAnchorElement>(null);

  // One reveal at a time: a double click must not spend two of the hourly reveals.
  async function reveal() {
    if (revealing) return;
    setRevealing(true);
    setError(false);
    try {
      setPhone((await api<{ phone: string }>(`/v1/listings/${shortId}/contact`, { method: 'POST' })).phone);
      setLimited(null);
    } catch (e) {
      if (e instanceof ApiError && e.code === 'rate_limited') setLimited(Math.max(1, Math.ceil((e.problem.retryAfter ?? 3600) / 60)));
      else setError(true);
    } finally {
      setRevealing(false);
    }
  }
  // The button is replaced by the number: move focus to the call link so keyboard users are not lost.
  useEffect(() => { if (phone) callRef.current?.focus(); }, [phone]);

  return (
    <div className="advertiser">
      <div className="adv-who">
        <div className="avatar" aria-hidden="true">{ownerName.slice(0, 1)}</div>
        <div>
          <div style={{ fontWeight: 600 }}>{ownerName}</div>
          <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
            <span className="badge b-green">{t('owner')}</span>
            <span className="badge b-muted">{t('not_verified')}</span>
          </div>
        </div>
      </div>
      {phone ? (
        <>
          <div className="phone-big" role="status">{phoneDisplay(lang, phone)}</div>
          <a ref={callRef} className="btn btn-primary" href={`tel:${phone}`}>{t('call')}</a>
        </>
      ) : (
        <>
          {limited != null && <p className="err" role="alert">{t('reveal_limited', { minutes: num(lang, limited) })}</p>}
          {error && <p className="err" role="alert">{t('error_generic')}</p>}
          {live && <button type="button" className="btn btn-primary" onClick={reveal} disabled={revealing}>{t('show_number')}</button>}
        </>
      )}
      <p className="warn-line">{t('pay_warn')}</p>
      {live && (
        <div className="side-actions">
          <SaveButton shortId={shortId} />
          <button type="button" className="linkish" onClick={() => setReporting(true)}>{t('report')}</button>
        </div>
      )}
      {reporting && <ReportDialog shortId={shortId} onClose={() => setReporting(false)} />}
    </div>
  );
}

const REASONS = ['suspicious', 'inaccurate', 'duplicate', 'unavailable'] as const;

function ReportDialog({ shortId, onClose }: { shortId: string; onClose: () => void }) {
  const t = useTranslations();
  const toast = useToast();
  const ref = useRef<HTMLDialogElement>(null);
  const [needReason, setNeedReason] = useState(false);
  const [failed, setFailed] = useState(false);
  const [busy, setBusy] = useState(false);

  useEffect(() => { if (ref.current && !ref.current.open) ref.current.showModal(); }, []);

  async function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    const reason = f.get('reason');
    setNeedReason(!reason);
    if (!reason) return;
    setBusy(true);
    try {
      await api(`/v1/listings/${shortId}/reports`, { method: 'POST', json: { reason, details: String(f.get('details') ?? '') || undefined } });
      ref.current?.close();
      toast(t('report_sent'));
    } catch {
      setFailed(true);
    } finally {
      setBusy(false);
    }
  }

  return (
    <dialog ref={ref} aria-labelledby="report-title" onClose={onClose}>
      <form className="dlg" noValidate onSubmit={submit}>
        <h2 id="report-title">{t('report_t')}</h2>
        <div className="radio-list" role="radiogroup" aria-labelledby="report-title">
          {REASONS.map(r => <label key={r}><input type="radio" name="reason" value={r} />{t(`r_${r}`)}</label>)}
        </div>
        {needReason && <p className="err" role="alert">{t('pick_reason')}</p>}
        <label className="f" htmlFor="r-details">{t('r_details')}</label>
        <textarea id="r-details" name="details" maxLength={500} />
        {failed && <p className="err" role="alert">{t('error_generic')}</p>}
        <div className="acts">
          <button type="button" className="btn btn-ghost" onClick={() => ref.current?.close()}>{t('cancel')}</button>
          <button className="btn btn-primary" type="submit" disabled={busy}>{t('send_report')}</button>
        </div>
      </form>
    </dialog>
  );
}

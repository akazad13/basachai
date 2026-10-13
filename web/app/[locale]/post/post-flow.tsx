'use client';

import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react';
import { useSearchParams } from 'next/navigation';
import { useLocale, useTranslations } from 'next-intl';
import { Link, useRouter } from '@/i18n/navigation';
import { api, ApiError } from '@/lib/api';
import { num, type Lang } from '@/lib/format';
import type { Area, Locations, Named, OwnerListing, Photo } from '@/lib/types';
import { CostBreakdown } from '@/components/cost-breakdown';
import { CheckIcon } from '@/components/icons';
import { ListingCard, areaName } from '@/components/listing-card';
import { useSession } from '@/components/session';
import { useToast } from '@/components/toast';
import { emptyDraft, firstIncompleteStep, fromListing, previewCard, toInput, validate, type Draft, type FieldErrors } from './form';
import { PhotoStep } from './photo-step';

/** API field names to form field names, for server-side validation errors. */
const FIELD: Record<string, keyof Draft | 'photos'> = {
  title: 'title', type: 'type', area: 'area', bedrooms: 'beds', bathrooms: 'baths', floorAreaSqft: 'sqft', floorNumber: 'floor',
  furnishing: 'furnish', occupancy: 'occ', description: 'desc', rules: 'rules', fullAddress: 'address',
  monthlyRent: 'rent', serviceCharge: 'service', advance: 'advance', others: 'others', amenities: 'amenities',
};
const STEP_OF: Partial<Record<keyof Draft | 'photos', number>> = { rent: 2, service: 2, advance: 2, others: 2, photos: 3 };
/** Fields that show their own error message; a server error on any other field is shown above the buttons. */
const SHOWN = new Set<keyof Draft | 'photos'>(['title', 'area', 'availDate', 'sqft', 'floor', 'desc', 'rules', 'address', 'rent', 'service', 'advance', 'others', 'photos']);

export function PostFlow() {
  const t = useTranslations();
  const lang = useLocale() as Lang;
  const router = useRouter();
  const toast = useToast();
  const draftId = useSearchParams().get('draft');
  const { me, ready, openSignIn, refresh, refreshAttention } = useSession();

  const [listing, setListing] = useState<OwnerListing | null>(null);
  const [draft, setDraft] = useState<Draft>(emptyDraft);
  const [photos, setPhotos] = useState<Photo[]>([]);
  const [step, setStep] = useState(1);
  const [errors, setErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState('');
  const [submitted, setSubmitted] = useState(false);
  const [busy, setBusy] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [areas, setAreas] = useState<Area[]>([]);
  const [amenities, setAmenities] = useState<Named[]>([]);
  const [loaded, setLoaded] = useState(!draftId);

  const live = listing?.status === 'published';
  const editable = !listing || ['draft', 'changes', 'published'].includes(listing.status);

  useEffect(() => {
    void api<Locations>('/v1/locations').then(l => setAreas(l.areas)).catch(() => {});
    void api<Named[]>('/v1/amenities').then(setAmenities).catch(() => {});
  }, []);

  useEffect(() => {
    if (ready && !me) openSignIn({ reason: 'signin_why_post', then: () => { void refresh(); } });
  }, [ready, me, openSignIn, refresh]);

  // Continue a draft at the first step that still needs something; editing anything else starts at step 1.
  // The draft this page created itself: its id lands in the URL, but reloading it would wipe what was typed since.
  const createdHere = useRef<string | null>(null);
  useEffect(() => {
    if (!draftId || !me || draftId === createdHere.current) return;
    api<OwnerListing>(`/v1/owner/listings/${draftId}`).then(l => {
      const d = fromListing(l);
      setListing(l); setDraft(d); setPhotos(l.photos);
      setStep(l.status === 'draft' ? firstIncompleteStep(d, l.photos.length) : 1);
      setLoaded(true);
    }, () => { setFormError(t('error_generic')); setLoaded(true); });
  }, [draftId, me, t]);

  // Autosave when leaving the page, for drafts and listings that need changes (never for Live listings: those save on demand).
  const latest = useRef({ draft, listing, step });
  latest.current = { draft, listing, step };
  useEffect(() => () => {
    const { draft: d, listing: l, step: s } = latest.current;
    if (l && (l.status === 'draft' || l.status === 'changes'))
      void api(`/v1/owner/listings/${l.id}`, { method: 'PATCH', json: toInput(d, Math.max(s, 2)), keepalive: true }).catch(() => {});
  }, []);

  const set = useCallback(<K extends keyof Draft>(key: K, value: Draft[K]) => {
    setDraft(d => ({ ...d, [key]: value }));
    setErrors(e => (e[key] ? { ...e, [key]: undefined } : e));
  }, []);

  function showErrors(e: FieldErrors) {
    setErrors(e);
    requestAnimationFrame(() => document.querySelector<HTMLElement>('[aria-invalid="true"]')?.focus());
  }

  function apiErrors(err: unknown) {
    if (err instanceof ApiError && err.code === 'validation_failed') {
      const e: FieldErrors = {};
      for (const [k, keys] of Object.entries(err.fields)) if (FIELD[k]) e[FIELD[k]] = keys[0];
      const first = Math.min(...Object.keys(e).map(k => STEP_OF[k as keyof Draft] ?? 1));
      if (Number.isFinite(first)) setStep(first);
      // Never fail silently: an error with nowhere to show it goes to the form message.
      if (Object.keys(err.fields).some(k => !FIELD[k] || !SHOWN.has(FIELD[k]))) setFormError(t('error_generic'));
      showErrors(e);
    } else if (err instanceof ApiError && err.code === 'photos_required') {
      setStep(3); showErrors({ photos: 'need_photos' });
    } else {
      setFormError(err instanceof ApiError && t.has(err.code as 'restricted') ? t(err.code as 'restricted') : t('error_generic'));
    }
  }

  /** Saves what is filled in so far. The first save of a valid step 1 creates the draft. */
  async function save(upTo: number): Promise<OwnerListing | null> {
    const input = toInput(draft, upTo);
    if (!listing) {
      if (Object.keys(validate(1, draft, 0)).length) return null;
      const created = await api<OwnerListing>('/v1/owner/listings', { method: 'POST', json: input });
      createdHere.current = created.id;
      setListing(created);
      if (!me?.roles.includes('owner')) void refresh();   // a first listing makes this person an owner, so the header shows My listings
      router.replace({ pathname: '/post', query: { draft: created.id } }, { scroll: false });
      return created;
    }
    // Live listings guard against a second tab overwriting the first. Drafts are autosaved by this page alone,
    // including on leave, so a version check there would only trip over our own earlier save.
    const updated = await api<OwnerListing>(`/v1/owner/listings/${listing.id}`, {
      method: 'PATCH', json: input, headers: live ? { 'If-Match': `"${listing.version}"` } : {},
    });
    setListing(updated);
    return updated;
  }

  async function next() {
    setFormError('');
    const e = validate(step, draft, photos.length);
    setBusy(true);
    try {
      // Each step is saved on Next even when it has errors, so nothing typed is lost (POST-8).
      if (!live && step <= 2) await save(step);
    } catch (err) { apiErrors(err); setBusy(false); return; }
    setBusy(false);
    if (Object.keys(e).length) { showErrors(e); return; }
    setErrors({});
    setStep(s => s + 1);
    window.scrollTo(0, 0);
  }

  async function saveLive() {
    setFormError('');
    for (const s of [1, 2]) {
      const e = validate(s, draft, photos.length);
      if (Object.keys(e).length) { setStep(s); showErrors(e); return; }
    }
    setBusy(true);
    try {
      await save(2);
      toast(t('t_saved_live'));
      router.push('/dashboard');
    } catch (err) {
      if (err instanceof ApiError && err.code === 'stale_listing') setFormError(t('stale_listing'));
      else apiErrors(err);
    } finally { setBusy(false); }
  }

  async function submit() {
    if (!listing) return;
    setBusy(true);
    try {
      await save(2);
      await api(`/v1/owner/listings/${listing.id}/submit`, { method: 'POST' });
      setSubmitted(true);
      void refreshAttention();
      window.scrollTo(0, 0);
    } catch (err) { apiErrors(err); } finally { setBusy(false); }
  }

  if (!ready || !me || !loaded) return <div style={{ padding: '40px 0' }} />;

  if (submitted) {
    return (
      <div className="success panel" style={{ margin: '32px 0' }}>
        <div className="mark"><CheckIcon /></div>
        <h1 style={{ fontSize: 26 }}>{t('submitted_t')}</h1>
        <p style={{ maxWidth: '46ch', margin: '10px auto 20px' }}>{t('submitted_d')}</p>
        <div style={{ display: 'flex', gap: 8, justifyContent: 'center', flexWrap: 'wrap' }}>
          <Link className="btn btn-primary" href="/dashboard">{t('go_dash')}</Link>
          <a className="btn btn-ghost" href={`/${lang}/post`}>{t('post_another')}</a>
        </div>
      </div>
    );
  }

  if (!editable) return <div className="empty" style={{ margin: '40px 0' }}><p>{t('error_generic')}</p></div>;

  const area = areas.find(a => a.slug === draft.area);
  const cover = photos[0] ? { publicId: photos[0].publicId, version: photos[0].version } : null;
  const preview = previewCard(draft, area, cover, photos.length);
  const err = (k: keyof Draft | 'photos') => !errors[k] ? ''
    : t.has(errors[k] as 'required') ? t(errors[k] as 'required', { n: num(lang, Math.max(0, 3 - photos.length)) }) : t('invalid');
  const inv = (k: keyof Draft) => ({ 'aria-invalid': errors[k] ? ('true' as const) : undefined, 'aria-describedby': errors[k] ? `e-${k}` : undefined });
  const errP = (k: keyof Draft) => errors[k] ? <p className="err" id={`e-${k}`}>{err(k)}</p> : null;

  let body: ReactNode;
  if (step === 1) {
    body = (
      <>
        <h2>{t('step1')}</h2>
        <div className="field">
          <label className="f" htmlFor="p-title">{t('l_title')}</label>
          <input id="p-title" value={draft.title} maxLength={90} placeholder={t('ph_title')} onChange={e => set('title', e.target.value)} {...inv('title')} />
          {errP('title')}
        </div>
        <div className="row2">
          <div className="field">
            <label className="f" htmlFor="p-type">{t('f_type')}</label>
            <select id="p-type" value={draft.type} onChange={e => set('type', e.target.value)}>
              <option value="flat">{t('type_flat')}</option><option value="house">{t('type_house')}</option>
            </select>
          </div>
          <div className="field">
            <label className="f" htmlFor="p-area">{t('f_area')}</label>
            <select id="p-area" value={draft.area} onChange={e => set('area', e.target.value)} {...inv('area')}>
              <option value="">{t('f_area')}</option>
              {areas.map(a => <option key={a.slug} value={a.slug}>{areaName(lang, a)}</option>)}
            </select>
            {errP('area')}
          </div>
        </div>
        <div className="row3">
          <div className="field">
            <label className="f" htmlFor="p-beds">{t('f_beds')}</label>
            <select id="p-beds" value={draft.beds} onChange={e => set('beds', e.target.value)}>
              <option value="0">{t('studio')}</option>
              {[1, 2, 3, 4, 5].map(n => <option key={n} value={n}>{num(lang, n)}</option>)}
            </select>
          </div>
          <div className="field">
            <label className="f" htmlFor="p-baths">{t('f_baths')}</label>
            <select id="p-baths" value={draft.baths} onChange={e => set('baths', e.target.value)}>
              {[1, 2, 3, 4].map(n => <option key={n} value={n}>{num(lang, n)}</option>)}
            </select>
          </div>
          <div className="field">
            <label className="f" htmlFor="p-sqft">{t('l_sqft')}</label>
            <input id="p-sqft" type="number" inputMode="numeric" min={0} value={draft.sqft} onChange={e => set('sqft', e.target.value)} {...inv('sqft')} />
            {errP('sqft')}
          </div>
        </div>
        <div className="row3">
          <div className="field">
            <label className="f" htmlFor="p-floor">{t('l_floor')}</label>
            <input id="p-floor" type="number" inputMode="numeric" min={0} max={50} value={draft.floor} onChange={e => set('floor', e.target.value)} {...inv('floor')} />
            {errP('floor')}
          </div>
          <div className="field">
            <label className="f" htmlFor="p-furnish">{t('f_furnish')}</label>
            <select id="p-furnish" value={draft.furnish} onChange={e => set('furnish', e.target.value)}>
              {(['unfurnished', 'semi', 'furnished'] as const).map(v => <option key={v} value={v}>{t(`furn_${v}`)}</option>)}
            </select>
          </div>
          <div className="field">
            <label className="f" htmlFor="p-occ">{t('f_occ')}</label>
            <select id="p-occ" value={draft.occ} onChange={e => set('occ', e.target.value)}>
              {(['any', 'family', 'bachelor', 'students'] as const).map(v => <option key={v} value={v}>{t(`occ_${v}`)}</option>)}
            </select>
          </div>
        </div>
        <fieldset className="field" style={{ border: 0, padding: 0, margin: '0 0 16px' }}>
          <legend className="f">{t('l_avail')}</legend>
          <div className="pills">
            <button type="button" aria-pressed={draft.availNow} onClick={() => set('availNow', true)}>{t('avail_now_f')}</button>
            <button type="button" aria-pressed={!draft.availNow} onClick={() => set('availNow', false)}>{t('l_avail_date')}</button>
          </div>
          {!draft.availNow && (
            <div style={{ marginTop: 8, maxWidth: 220 }}>
              <label className="sr" htmlFor="p-date">{t('l_avail_date')}</label>
              <input id="p-date" type="date" value={draft.availDate} onChange={e => set('availDate', e.target.value)} {...inv('availDate')} />
              {errP('availDate')}
            </div>
          )}
        </fieldset>
        <fieldset className="field" style={{ border: 0, padding: 0, margin: '0 0 16px' }}>
          <legend className="f">{t('l_amen')}</legend>
          <div className="checks">
            {amenities.map(a => (
              <label className="check" key={a.key}>
                <input type="checkbox" checked={draft.amenities.includes(a.key)}
                  onChange={e => set('amenities', e.target.checked ? [...draft.amenities, a.key] : draft.amenities.filter(k => k !== a.key))} />
                {lang === 'bn' ? a.nameBn : a.nameEn}
              </label>
            ))}
          </div>
        </fieldset>
        <div className="field">
          <label className="f" htmlFor="p-desc">{t('l_desc')}</label>
          <textarea id="p-desc" maxLength={2000} value={draft.desc} placeholder={t('ph_desc')} onChange={e => set('desc', e.target.value)} {...inv('desc')} />
          {errP('desc')}
        </div>
        <div className="field">
          <label className="f" htmlFor="p-rules">{t('l_rules')}</label>
          <input id="p-rules" maxLength={300} value={draft.rules} placeholder={t('ph_rules')} onChange={e => set('rules', e.target.value)} {...inv('rules')} />
          {errP('rules')}
        </div>
        <div className="field">
          <label className="f" htmlFor="p-address">{t('l_address')}</label>
          <input id="p-address" maxLength={300} value={draft.address} onChange={e => set('address', e.target.value)}
            aria-invalid={errors.address ? 'true' : undefined} aria-describedby={errors.address ? 'h-address e-address' : 'h-address'} />
          <p className="hint" id="h-address">{t('address_hint')}</p>
          {errP('address')}
        </div>
      </>
    );
  } else if (step === 2) {
    body = (
      <>
        <h2>{t('step2')}</h2>
        <div className="row2">
          <div className="field">
            <label className="f" htmlFor="p-rent">{t('l_rent')}</label>
            <input id="p-rent" type="number" inputMode="numeric" min={0} step={100} value={draft.rent} onChange={e => set('rent', e.target.value)} {...inv('rent')} />
            {errP('rent')}
          </div>
          <div className="field">
            <label className="f" htmlFor="p-service">{t('l_service')}</label>
            <input id="p-service" type="number" inputMode="numeric" min={0} step={50} value={draft.service} onChange={e => set('service', e.target.value)}
              aria-invalid={errors.service ? 'true' : undefined} aria-describedby={errors.service ? 'h-service e-service' : 'h-service'} />
            <p className="hint" id="h-service">{t('service_hint')}</p>
            {errP('service')}
          </div>
        </div>
        <div className="field">
          <label className="f" htmlFor="p-advance">{t('l_advance')}</label>
          <input id="p-advance" type="number" inputMode="numeric" min={0} step={500} style={{ maxWidth: 260 }} value={draft.advNone ? '' : draft.advance}
            disabled={draft.advNone} onChange={e => set('advance', e.target.value)} {...inv('advance')} />
          {errP('advance')}
          <label className="check" style={{ marginTop: 8 }}>
            <input type="checkbox" checked={draft.advNone} onChange={e => { set('advNone', e.target.checked); set('advance', ''); }} />
            {t('l_adv_none')}
          </label>
        </div>
        <fieldset className="field" style={{ border: 0, padding: 0, margin: '0 0 16px' }}>
          <legend className="f">{t('l_others')}</legend>
          <p className="hint">{t('others_hint')}</p>
          {errP('others')}
          {draft.others.map((o, i) => (
            <div className="other-row" key={i}>
              <input aria-label={t('other_label')} placeholder={t('other_label')} value={o.label} maxLength={60}
                onChange={e => set('others', draft.others.map((x, k) => k === i ? { ...x, label: e.target.value } : x))} />
              <input type="number" inputMode="numeric" min={0} aria-label={t('other_amount')} placeholder={t('other_amount')} value={o.amount}
                onChange={e => set('others', draft.others.map((x, k) => k === i ? { ...x, amount: e.target.value } : x))} />
              <select aria-label={`${t('monthly')} / ${t('once')}`} value={o.period}
                onChange={e => set('others', draft.others.map((x, k) => k === i ? { ...x, period: e.target.value as 'monthly' | 'one_time' } : x))}>
                <option value="monthly">{t('monthly')}</option><option value="one_time">{t('once')}</option>
              </select>
              <button type="button" className="icon-btn" aria-label={t('remove')} onClick={() => set('others', draft.others.filter((_, k) => k !== i))}>×</button>
            </div>
          ))}
          {draft.others.length < 10 && (
            <button type="button" className="btn btn-ghost btn-sm" onClick={() => set('others', [...draft.others, { label: '', amount: '', period: 'monthly' }])}>{t('add_cost')}</button>
          )}
        </fieldset>
      </>
    );
  } else if (step === 3) {
    body = <PhotoStep listingId={listing!.id} photos={photos} onChange={p => { setPhotos(p); setErrors({}); }} needError={err('photos')} onBusy={setUploading} />;
  } else {
    body = (
      <>
        <h2>{t('step4')}</h2>
        <div className="cards"><ListingCard card={preview.card} preview /></div>
        <p className="hint" style={{ marginTop: 14 }}>{t('review_note')}</p>
      </>
    );
  }

  return (
    <>
      <div style={{ paddingTop: 24 }}>
        <h1 style={{ fontSize: 30 }}>{t('post_t')}</h1>
        <p className="hint" style={{ fontSize: 15 }}>{t('post_sub')}</p>
        <ol className="stepper">
          {(['step1', 'step2', 'step3', 'step4'] as const).map((k, i) => (
            <li key={k} aria-current={i + 1 === step ? 'step' : undefined} className={i + 1 < step ? 'done' : undefined}>
              <span className="n">{num(lang, i + 1)}</span>{t(k)}
            </li>
          ))}
        </ol>
      </div>
      {listing?.status === 'changes' && listing.moderatorReason && (
        <div className="reason"><strong>{t('mod_reason')}:</strong> {listing.moderatorReason}</div>
      )}
      <div className="post" style={{ paddingTop: 0 }}>
        <div>
          <div className="panel">{body}</div>
          {formError && <p className="form-error" role="alert">{formError}</p>}
          <div className="form-nav">
            {step > 1 ? <button type="button" className="btn btn-ghost" onClick={() => { setErrors({}); setStep(s => s - 1); }}>{t('prev')}</button> : <span />}
            <span style={{ display: 'flex', gap: 8, flexWrap: 'wrap', justifyContent: 'flex-end' }}>
              {live && <button type="button" className="btn btn-brass" disabled={busy} onClick={saveLive}>{t('save_changes')}</button>}
              {step < 4 && <button type="button" className="btn btn-primary" disabled={busy || uploading} onClick={next}>{t('next')}</button>}
              {step === 4 && !live && <button type="button" className="btn btn-brass" disabled={busy} onClick={submit}>{t('submit')}</button>}
            </span>
          </div>
        </div>
        <aside className="preview">
          <p className="f" style={{ marginBottom: 8 }}>{t('preview_t')}</p>
          <CostBreakdown card={preview.card} oneTime={preview.oneTime} />
        </aside>
      </div>
    </>
  );
}

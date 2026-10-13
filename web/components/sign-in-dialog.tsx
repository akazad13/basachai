'use client';

import { useEffect, useRef, useState, type FormEvent } from 'react';
import { useLocale, useTranslations } from 'next-intl';
import { api, ApiError } from '@/lib/api';
import { digits, isValidPhone, num, type Lang } from '@/lib/format';

type Props = {
  reason?: 'signin_why_save' | 'signin_why_post';
  onClose: () => void;
  onSignedIn: () => void | Promise<void>;
};

/** Sign in or create an account with a mobile number and password. Nothing is ever sent to the phone (D9). */
export function SignInDialog({ reason, onClose, onSignedIn }: Props) {
  const t = useTranslations();
  const lang = useLocale() as Lang;
  const ref = useRef<HTMLDialogElement>(null);
  const [signup, setSignup] = useState(false);
  const [needCode, setNeedCode] = useState(false);
  const [busy, setBusy] = useState(false);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState('');

  useEffect(() => {
    const d = ref.current;
    if (d && !d.open) d.showModal();
    d?.querySelector<HTMLInputElement>('input')?.focus();
  }, [signup]);

  async function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    const name = String(f.get('name') ?? '').trim();
    const phone = String(f.get('phone') ?? '');
    const password = String(f.get('password') ?? '');
    const code = String(f.get('code') ?? '');
    const next: Record<string, string> = {};
    if (signup && (name.length < 2 || name.length > 60)) next.name = t('name_length');
    if (!isValidPhone(phone)) next.phone = t('phone_err');
    if (password.length < 8) next.password = t('pass_err');
    setErrors(next);
    setFormError('');
    if (Object.keys(next).length) {
      ref.current?.querySelector<HTMLInputElement>('[aria-invalid="true"]')?.focus();
      return;
    }
    setBusy(true);
    try {
      if (signup) await api('/v1/auth/register', { method: 'POST', json: { name, phone, password } });
      else await api('/v1/auth/login', { method: 'POST', json: { phone, password, code: code || undefined } });
      await onSignedIn();
    } catch (err) {
      if (!(err instanceof ApiError)) { setFormError(t('error_generic')); return; }
      if (err.code === 'two_factor_code_required') {
        setNeedCode(true);
        setFormError(t('two_factor_code_required'));
        requestAnimationFrame(() => ref.current?.querySelector<HTMLInputElement>('#s-code')?.focus());
        return;
      }
      if (err.code === 'phone_taken') { setErrors({ phone: t('phone_taken') }); return; }
      if (err.code === 'validation_failed') {
        setErrors(Object.fromEntries(Object.entries(err.fields).map(([k, keys]) => [k, t(keys[0] as never)])));
        return;
      }
      const minutes = num(lang, Math.ceil((err.problem.retryAfter ?? 900) / 60));
      setFormError(err.code === 'rate_limited' ? t('locked', { minutes }) : t.has(err.code as never) ? t(err.code as never) : t('error_generic'));
    } finally {
      setBusy(false);
    }
  }

  const field = (name: string, label: string, type: string, autoComplete: string, extra: Record<string, string> = {}) => (
    <div className="field">
      <label className="f" htmlFor={`s-${name}`}>{label}</label>
      <input id={`s-${name}`} name={name} type={type} autoComplete={autoComplete} aria-invalid={errors[name] ? 'true' : undefined}
        aria-describedby={errors[name] ? `s-${name}-err` : undefined} {...extra} />
      {errors[name] && <p className="err" id={`s-${name}-err`}>{errors[name]}</p>}
    </div>
  );

  return (
    <dialog ref={ref} aria-labelledby="dlg-title" onClose={onClose} onCancel={onClose}>
      <form className="dlg" noValidate onSubmit={submit}>
        <h2 id="dlg-title">{t(signup ? 'create_account' : 'signin_t')}</h2>
        {reason && <p>{t(reason)}</p>}
        {signup && field('name', t('name'), 'text', 'name')}
        {field('phone', t('phone'), 'tel', 'tel', { inputMode: 'tel', placeholder: digits(lang, '01712345678') })}
        {field('password', t('password'), 'password', signup ? 'new-password' : 'current-password')}
        {needCode && !signup && field('code', t('code_label'), 'text', 'one-time-code', { inputMode: 'numeric' })}
        <p className="hint">{t('signin_note')}</p>
        {formError && <p className="err" role="alert">{formError}</p>}
        <p>
          <button type="button" className="linkish" onClick={() => { setSignup(s => !s); setErrors({}); setFormError(''); }}>
            {t(signup ? 'signin_btn' : 'create_account')}
          </button>
        </p>
        <div className="acts">
          <button type="button" className="btn btn-ghost" onClick={() => ref.current?.close()}>{t('cancel')}</button>
          <button className="btn btn-primary" type="submit" disabled={busy}>{t(signup ? 'create_account' : 'signin_btn')}</button>
        </div>
      </form>
    </dialog>
  );
}

'use client';

import { useEffect, useRef, useState, type FormEvent } from 'react';
import { useLocale, useTranslations } from 'next-intl';
import { usePathname, useRouter } from '@/i18n/navigation';
import { api, ApiError } from '@/lib/api';
import { phoneDisplay, type Lang } from '@/lib/format';
import { useSession } from '@/components/session';
import { useToast } from '@/components/toast';

/** Profile, language, password, sign out and account deletion (auth-and-account spec). */
export default function AccountPage() {
  const t = useTranslations();
  const lang = useLocale() as Lang;
  const toast = useToast();
  const router = useRouter();
  const pathname = usePathname();
  const { me, ready, refresh, signOut, openSignIn } = useSession();
  const [nameError, setNameError] = useState('');
  const [pwErrors, setPwErrors] = useState<Record<string, string>>({});
  const [confirmDelete, setConfirmDelete] = useState(false);
  const deleteRef = useRef<HTMLDialogElement>(null);
  // Set while signing out or deleting, so the page does not ask the person to sign in again on the way out.
  const leaving = useRef(false);

  useEffect(() => { if (ready && !me && !leaving.current) openSignIn(); }, [ready, me, openSignIn]);
  useEffect(() => { if (confirmDelete && deleteRef.current && !deleteRef.current.open) deleteRef.current.showModal(); }, [confirmDelete]);

  if (!ready || !me) return <div style={{ padding: '40px 0' }} />;

  async function saveProfile(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    const name = String(f.get('name') ?? '').trim();
    const locale = String(f.get('locale'));
    if (name.length < 2 || name.length > 60) { setNameError(t('name_length')); return; }
    setNameError('');
    try {
      await api('/v1/me', { method: 'PATCH', json: { name, locale } });
    } catch (err) {
      if (err instanceof ApiError && err.fields.name) setNameError(t(err.fields.name[0] as 'name_length'));
      else toast(t('error_generic'));
      return;
    }
    await refresh();
    toast(t('t_profile_saved'));
    if (locale !== lang) router.replace(pathname, { locale: locale as Lang });
  }

  async function changePassword(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const form = e.currentTarget;
    const f = new FormData(form);
    const next = String(f.get('newPassword') ?? '');
    if (next.length < 8) { setPwErrors({ newPassword: t('pass_err') }); return; }
    try {
      await api('/v1/auth/password/change', { method: 'POST', json: { currentPassword: f.get('currentPassword'), newPassword: next } });
      setPwErrors({});
      form.reset();
      toast(t('t_password_changed'));
    } catch (err) {
      if (err instanceof ApiError && err.code === 'validation_failed')
        setPwErrors(Object.fromEntries(Object.entries(err.fields).map(([k, v]) => [k, t(v[0] as 'pass_err')])));
      else setPwErrors({ form: t('error_generic') });
    }
  }

  async function deleteAccount() {
    leaving.current = true;
    try { await api('/v1/me', { method: 'DELETE' }); }
    catch { leaving.current = false; deleteRef.current?.close(); toast(t('error_generic')); return; }
    router.replace('/');
    await refresh();
  }

  async function leave() {
    leaving.current = true;
    try { await signOut(); }
    catch { leaving.current = false; toast(t('error_generic')); return; }
    router.replace('/');
  }

  return (
    <div className="dash" style={{ maxWidth: 640 }}>
      <div className="dash-head"><h1 style={{ fontSize: 28 }}>{t('account_t')}</h1></div>

      <section className="panel" aria-labelledby="profile-t" style={{ marginBottom: 16 }}>
        <h2 id="profile-t">{t('profile_t')}</h2>
        <form onSubmit={saveProfile} noValidate>
          <div className="field">
            <label className="f" htmlFor="a-name">{t('name')}</label>
            <input id="a-name" name="name" defaultValue={me.name} autoComplete="name" aria-invalid={nameError ? 'true' : undefined} />
            {nameError && <p className="err">{nameError}</p>}
          </div>
          <div className="field">
            <label className="f" htmlFor="a-phone">{t('phone')}</label>
            <input id="a-phone" value={phoneDisplay(lang, me.phone)} readOnly aria-describedby="a-phone-h" />
            <p className="hint" id="a-phone-h">{t('phone_readonly')}</p>
          </div>
          <div className="field">
            <label className="f" htmlFor="a-locale">{t('language')}</label>
            <select id="a-locale" name="locale" defaultValue={me.locale}>
              {/* Each language is named in its own script whatever the page language, so these two are not translated. */}
              {/* eslint-disable-next-line i18next/no-literal-string */}
              <option value="bn">বাংলা</option>
              {/* eslint-disable-next-line i18next/no-literal-string */}
              <option value="en">English</option>
            </select>
          </div>
          <button className="btn btn-primary" type="submit">{t('save_changes')}</button>
        </form>
      </section>

      <section className="panel" aria-labelledby="pw-t" style={{ marginBottom: 16 }}>
        <h2 id="pw-t">{t('password_t')}</h2>
        <form onSubmit={changePassword} noValidate>
          <div className="field">
            <label className="f" htmlFor="a-cur">{t('current_password')}</label>
            <input id="a-cur" name="currentPassword" type="password" autoComplete="current-password" aria-invalid={pwErrors.currentPassword ? 'true' : undefined} />
            {pwErrors.currentPassword && <p className="err">{pwErrors.currentPassword}</p>}
          </div>
          <div className="field">
            <label className="f" htmlFor="a-new">{t('new_password')}</label>
            <input id="a-new" name="newPassword" type="password" autoComplete="new-password" aria-invalid={pwErrors.newPassword ? 'true' : undefined} />
            {pwErrors.newPassword && <p className="err">{pwErrors.newPassword}</p>}
          </div>
          {pwErrors.form && <p className="err" role="alert">{pwErrors.form}</p>}
          <button className="btn btn-primary" type="submit">{t('change_password')}</button>
        </form>
      </section>

      <section className="panel" aria-labelledby="out-t" style={{ marginBottom: 16 }}>
        <h2 id="out-t" className="sr">{t('sign_out')}</h2>
        <button type="button" className="btn btn-ghost" onClick={leave}>{t('sign_out')}</button>
      </section>

      <section className="panel" aria-labelledby="del-t">
        <h2 id="del-t">{t('delete_t')}</h2>
        <p>{t('delete_d')}</p>
        <button type="button" className="btn btn-danger" onClick={() => setConfirmDelete(true)}>{t('delete_t')}</button>
      </section>

      {confirmDelete && (
        <dialog ref={deleteRef} aria-labelledby="del-dlg" onClose={() => setConfirmDelete(false)}>
          <div className="dlg">
            <h2 id="del-dlg">{t('delete_t')}</h2>
            <p>{t('delete_d')}</p>
            <div className="acts">
              <button type="button" className="btn btn-ghost" onClick={() => deleteRef.current?.close()}>{t('cancel')}</button>
              <button type="button" className="btn btn-danger" onClick={deleteAccount}>{t('delete_confirm')}</button>
            </div>
          </div>
        </dialog>
      )}
    </div>
  );
}

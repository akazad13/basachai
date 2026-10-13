'use client';

import { Suspense, useEffect, useState, type FormEvent } from 'react';
import { useSearchParams } from 'next/navigation';
import { useTranslations } from 'next-intl';
import { useRouter } from '@/i18n/navigation';
import { api, ApiError } from '@/lib/api';
import { useSession } from '@/components/session';

/** Opens from the single-use link an admin issued (D10). Setting the password signs this device in and ends every other session. */
export default function ResetPage() {
  return <Suspense><ResetForm /></Suspense>;
}

function ResetForm() {
  const t = useTranslations();
  const router = useRouter();
  const token = useSearchParams().get('token') ?? '';
  const { refresh } = useSession();
  const [valid, setValid] = useState<boolean | null>(null);
  const [error, setError] = useState('');

  useEffect(() => {
    api<{ valid: boolean }>(`/v1/auth/password/reset/check?token=${encodeURIComponent(token)}`)
      .then(r => setValid(r.valid), () => setValid(false));
  }, [token]);

  async function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const password = String(new FormData(e.currentTarget).get('newPassword') ?? '');
    if (password.length < 8) { setError(t('pass_err')); return; }
    try {
      await api('/v1/auth/password/reset/confirm', { method: 'POST', json: { token, newPassword: password } });
      const me = await refresh();
      router.replace(me?.roles.includes('owner') ? '/dashboard' : '/');
    } catch (err) {
      if (err instanceof ApiError && err.code === 'reset_used') { setValid(false); return; }
      setError(err instanceof ApiError && err.code === 'validation_failed'
        ? t((Object.values(err.fields)[0]?.[0] ?? 'pass_err') as 'pass_err') : t('error_generic'));
    }
  }

  return (
    <div className="dash" style={{ maxWidth: 520 }}>
      <div className="dash-head"><h1 style={{ fontSize: 28 }}>{t('reset_t')}</h1></div>
      {valid === false && <div className="empty"><p role="alert">{t('reset_used')}</p></div>}
      {valid && (
        <form className="panel" onSubmit={submit} noValidate>
          <div className="field">
            <label className="f" htmlFor="r-new">{t('new_password')}</label>
            <input id="r-new" name="newPassword" type="password" autoComplete="new-password" aria-invalid={error ? 'true' : undefined} />
            {error && <p className="err" role="alert">{error}</p>}
          </div>
          <button className="btn btn-primary" type="submit">{t('set_password')}</button>
        </form>
      )}
    </div>
  );
}

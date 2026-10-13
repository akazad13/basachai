'use client';

import { useEffect, useState, type FormEvent } from 'react';
import { useTranslations } from 'next-intl';
import { useRouter } from '@/i18n/navigation';
import { api, ApiError } from '@/lib/api';
import { useSession } from '@/components/session';

type Setup = { enabled: boolean; sharedKey: string | null; uri: string | null };

/** Staff must turn on authenticator-app sign-in before using moderation pages (AUTH-7). */
export default function TwoFactorPage() {
  const t = useTranslations();
  const router = useRouter();
  const { me, ready, refresh, openSignIn } = useSession();
  const [setup, setSetup] = useState<Setup | null>(null);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    if (!ready) return;
    if (!me) { openSignIn(); return; }
    api<Setup>('/v1/me/two-factor').then(setSetup, () => setFailed(true));
  }, [ready, me, openSignIn]);

  async function enable(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    try {
      await api('/v1/me/two-factor', { method: 'POST', json: { code: f.get('code'), password: f.get('password') } });
      await refresh();
      router.replace('/admin');
    } catch (err) {
      if (err instanceof ApiError && err.code === 'validation_failed')
        setErrors(Object.fromEntries(Object.entries(err.fields).map(([k, v]) => [k, t(v[0] as 'bad_code')])));
      else setErrors({ form: t('error_generic') });
    }
  }

  if (failed) return <div className="empty" role="alert" style={{ margin: '40px 0' }}><p>{t('error_generic')}</p></div>;
  if (!setup) return <div style={{ padding: '40px 0' }} />;
  return (
    <div className="dash" style={{ maxWidth: 560 }}>
      <div className="dash-head"><h1 style={{ fontSize: 28 }}>{t('tf_t')}</h1></div>
      {setup.enabled ? <p role="status">{t('tf_done')}</p> : (
        <form className="panel" onSubmit={enable} noValidate>
          <p>{t('tf_d')}</p>
          <p className="f">{t('tf_key')}</p>
          <p className="key-box">{setup.sharedKey}</p>
          <div className="field">
            <label className="f" htmlFor="tf-password">{t('password')}</label>
            <input id="tf-password" name="password" type="password" autoComplete="current-password" aria-invalid={errors.password ? 'true' : undefined} />
            {errors.password && <p className="err">{errors.password}</p>}
          </div>
          <div className="field">
            <label className="f" htmlFor="tf-code">{t('tf_code')}</label>
            <input id="tf-code" name="code" inputMode="numeric" autoComplete="one-time-code" aria-invalid={errors.code ? 'true' : undefined} />
            {errors.code && <p className="err">{errors.code}</p>}
          </div>
          {errors.form && <p className="err" role="alert">{errors.form}</p>}
          <button className="btn btn-primary" type="submit">{t('tf_enable')}</button>
        </form>
      )}
    </div>
  );
}

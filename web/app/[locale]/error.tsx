'use client';

import { useEffect } from 'react';
import { useTranslations } from 'next-intl';
import { Link } from '@/i18n/navigation';

/** Shown when a page fails to load (for example the API is down), instead of the framework's error page. */
export default function PageError({ error, reset }: { error: Error & { digest?: string }; reset: () => void }) {
  const t = useTranslations();
  useEffect(() => { console.error(error); }, [error]);
  return (
    <div className="empty" role="alert" style={{ margin: '40px 0' }}>
      <h1 style={{ fontSize: 26 }}>{t('error_generic')}</h1>
      <div className="actions">
        <button type="button" className="btn btn-primary" onClick={reset}>{t('retry')}</button>
        <Link className="btn btn-ghost" href="/">{t('go_home')}</Link>
      </div>
    </div>
  );
}

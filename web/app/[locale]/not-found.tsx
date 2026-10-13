import { useTranslations } from 'next-intl';
import { Link } from '@/i18n/navigation';

export default function NotFound() {
  const t = useTranslations();
  return (
    <div className="empty" style={{ margin: '40px 0' }}>
      <h1 style={{ fontSize: 26 }}>{t('page_not_found')}</h1>
      <div className="actions"><Link className="btn btn-primary" href="/">{t('go_home')}</Link></div>
    </div>
  );
}

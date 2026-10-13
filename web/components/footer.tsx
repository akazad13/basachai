import { useTranslations } from 'next-intl';
import { Link } from '@/i18n/navigation';

export function Footer() {
  const t = useTranslations();
  return (
    <footer className="site">
      <div className="wrap">
        <ul>
          <li><Link href="/about">{t('f_about')}</Link></li>
          <li><Link href="/how-it-works">{t('f_how')}</Link></li>
          <li><Link href="/safety">{t('f_safety')}</Link></li>
          <li><Link href="/support">{t('f_support')}</Link></li>
          <li><Link href="/terms">{t('f_terms')}</Link></li>
          <li><Link href="/privacy">{t('f_privacy')}</Link></li>
        </ul>
        <p>{t('f_line')}</p>
        <p className="hint">{t('f_photo_credit')}</p>
      </div>
    </footer>
  );
}

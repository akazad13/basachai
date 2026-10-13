'use client';

import { useEffect, type ReactNode } from 'react';
import { useTranslations } from 'next-intl';
import { Link, usePathname, useRouter } from '@/i18n/navigation';
import { useSession } from '@/components/session';

/** Staff only. Moderators and admins without two-factor sign-in are sent to set it up first (AUTH-7). */
export default function AdminLayout({ children }: { children: ReactNode }) {
  const t = useTranslations();
  const router = useRouter();
  const pathname = usePathname();
  const { me, ready, isStaff, openSignIn } = useSession();
  const isAdmin = !!me?.roles.includes('admin');

  useEffect(() => {
    if (!ready) return;
    if (!me) openSignIn();
    else if (isStaff && !me.twoFactorEnabled) router.replace('/account/two-factor');
  }, [ready, me, isStaff, openSignIn, router]);

  if (!ready || !me || (isStaff && !me.twoFactorEnabled)) return <div style={{ padding: '40px 0' }} />;
  if (!isStaff) return <div className="empty" style={{ margin: '40px 0' }}><p>{t('page_not_found')}</p></div>;

  const tab = (href: string, label: string) => (
    <Link href={href} aria-current={(href === '/admin' ? pathname === href : pathname.startsWith(href)) ? 'page' : undefined}>{label}</Link>
  );
  return (
    <div className="dash">
      <nav className="tabs" aria-label={t('admin_t')}>
        {tab('/admin', t('admin_t'))}
        {tab('/admin/listings', t('listings_t'))}
        {isAdmin && tab('/admin/users', t('users_t'))}
        {isAdmin && tab('/admin/audit', t('audit_t'))}
        {isAdmin && tab('/admin/config', t('config_t'))}
      </nav>
      {children}
    </div>
  );
}

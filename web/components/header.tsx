'use client';

import { useLocale, useTranslations } from 'next-intl';
import { useSearchParams } from 'next/navigation';
import { Link, usePathname, useRouter } from '@/i18n/navigation';
import { num, type Lang } from '@/lib/format';
import { api } from '@/lib/api';
import { HeartIcon, ListIcon, PlusIcon, RoofIcon, SearchIcon, ShieldIcon, UserIcon } from './icons';
import { useSession } from './session';

/** Links by role. Below 760 px the nav becomes a bottom tab bar (prototype CSS). */
export function Header() {
  const t = useTranslations();
  const lang = useLocale() as Lang;
  const other: Lang = lang === 'bn' ? 'en' : 'bn';
  const pathname = usePathname();
  const search = useSearchParams();
  const router = useRouter();
  const { me, ready, isOwner, isStaff, attention, openSignIn } = useSession();
  const current = (p: string) => (pathname === p || pathname.startsWith(p + '/') ? 'page' : undefined);

  async function switchLanguage() {
    const query = Object.fromEntries(search.entries());
    if (me) void api('/v1/me', { method: 'PATCH', json: { locale: other } }).catch(() => {});
    router.replace({ pathname, query }, { locale: other });
  }

  function post() {
    if (me) router.push('/post');
    else openSignIn({ reason: 'signin_why_post', then: () => router.push('/post') });
  }

  return (
    <header className="site">
      <div className="wrap">
        <Link className="logo" href="/"><RoofIcon /><span lang="bn">{t('brand')}</span></Link>
        <button type="button" className="lang" lang={other} onClick={switchLanguage}>{t('lang_switch')}</button>
        <nav className="main" aria-label="Main">
          <Link href="/search" aria-current={current('/search')}><SearchIcon />{t('nav_find')}</Link>
          {isStaff && <Link href="/admin" aria-current={current('/admin')}><ShieldIcon />{t('nav_admin')}</Link>}
          {me && !isStaff && <Link href="/saved" aria-current={current('/saved')}><HeartIcon />{t('nav_saved')}</Link>}
          {isOwner && (
            <Link href="/dashboard" aria-current={current('/dashboard')}>
              <ListIcon />{t('nav_dash')}
              {attention > 0 && <span className="count">{num(lang, attention)}</span>}
            </Link>
          )}
          {!isStaff && (
            <button type="button" className="link btn btn-primary btn-sm" onClick={post}><PlusIcon />{t('nav_post')}</button>
          )}
          {ready && !me && <button type="button" className="link" onClick={() => openSignIn()}><UserIcon />{t('nav_signin')}</button>}
          {me && <Link href="/account" aria-current={current('/account')}><UserIcon />{t('nav_account')}</Link>}
        </nav>
      </div>
    </header>
  );
}

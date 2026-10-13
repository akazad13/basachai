'use client';

import { Suspense, useCallback, useEffect, useState, type FormEvent } from 'react';
import { useSearchParams } from 'next/navigation';
import { useLocale, useTranslations } from 'next-intl';
import { usePathname, useRouter } from '@/i18n/navigation';
import { api, ApiError } from '@/lib/api';
import { num, phoneDisplay, type Lang } from '@/lib/format';
import type { AdminUser } from '@/lib/types';
import { useToast } from '@/components/toast';

export default function UsersPage() {
  return <Suspense><Users /></Suspense>;
}

/** Admin only: find people, restrict or suspend them, and issue single-use password reset links (D10). */
function Users() {
  const t = useTranslations();
  const lang = useLocale() as Lang;
  const toast = useToast();
  const router = useRouter();
  const pathname = usePathname();
  const q = useSearchParams().get('q') ?? '';
  const [users, setUsers] = useState<AdminUser[] | null>(null);
  const [reset, setReset] = useState<{ userId: string; link: string } | null>(null);

  const load = useCallback(async () => {
    setUsers(await api<AdminUser[]>(`/v1/admin/users${q ? '?q=' + encodeURIComponent(q) : ''}`));
  }, [q]);
  useEffect(() => { void load().catch(() => toast(t('error_generic'))); }, [load, toast, t]);

  function search(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const v = String(new FormData(e.currentTarget).get('q') ?? '').trim();
    router.replace(v ? `${pathname}?q=${encodeURIComponent(v)}` : pathname);
  }

  async function setStatus(u: AdminUser, action: 'restrict' | 'suspend' | 'reinstate') {
    try {
      await api(`/v1/admin/users/${u.id}/${action}`, { method: 'POST' });
      toast(t('t_user_updated'));
      await load();
    } catch (e) { toast(e instanceof ApiError && e.code === 'validation_failed' ? t('invalid') : t('error_generic')); }
  }

  async function issueReset(u: AdminUser) {
    try {
      const r = await api<{ link: string }>(`/v1/admin/users/${u.id}/issue-password-reset`, { method: 'POST' });
      setReset({ userId: u.id, link: r.link });
      toast(t('t_reset_issued'));
    } catch { toast(t('error_generic')); }
  }

  return (
    <>
      <div className="dash-head"><h1 style={{ fontSize: 28 }}>{t('users_t')}</h1></div>
      <form className="search-card" onSubmit={search} style={{ marginBottom: 16 }}>
        <div className="field">
          <label className="f" htmlFor="u-q">{t('search_users')}</label>
          <input id="u-q" name="q" defaultValue={q} key={q} />
        </div>
        <button className="btn btn-primary" type="submit">{t('search_btn')}</button>
      </form>
      <div className="table-wrap">
        <table className="data">
          <thead><tr><th>{t('name')}</th><th>{t('phone')}</th><th>{t('status_t')}</th><th>{t('listings_t')}</th><th /></tr></thead>
          <tbody>
            {users?.map(u => (
              <tr key={u.id}>
                <td>{u.name}<div className="hint">{u.roles.join(', ')}</div></td>
                <td>{phoneDisplay(lang, u.phone)}</td>
                <td>{t(`us_${u.status}`)}</td>
                <td>{num(lang, u.listings)}</td>
                <td>
                  <div className="acts" style={{ marginTop: 0 }}>
                    {u.status !== 'restricted' && <button type="button" className="btn btn-ghost btn-sm" onClick={() => setStatus(u, 'restrict')}>{t('restrict')}</button>}
                    {u.status !== 'suspended' && <button type="button" className="btn btn-danger btn-sm" onClick={() => setStatus(u, 'suspend')}>{t('suspend')}</button>}
                    {u.status !== 'active' && <button type="button" className="btn btn-ghost btn-sm" onClick={() => setStatus(u, 'reinstate')}>{t('reinstate')}</button>}
                    <button type="button" className="btn btn-ghost btn-sm" onClick={() => issueReset(u)}>{t('issue_reset')}</button>
                  </div>
                  {reset?.userId === u.id && (
                    <div className="field" style={{ marginTop: 8 }}>
                      <label className="f" htmlFor={`reset-${u.id}`}>{t('reset_link')}</label>
                      <input id={`reset-${u.id}`} readOnly value={reset.link} onFocus={e => e.target.select()} />
                      <button type="button" className="btn btn-ghost btn-sm" style={{ marginTop: 6 }}
                        onClick={() => navigator.clipboard?.writeText(reset.link).then(() => toast(t('copied')))}>{t('copy')}</button>
                    </div>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

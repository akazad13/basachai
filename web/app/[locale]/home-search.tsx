'use client';

import type { FormEvent } from 'react';
import { useLocale, useTranslations } from 'next-intl';
import { useRouter } from '@/i18n/navigation';
import { num, toLatinDigits, type Lang } from '@/lib/format';
import type { Area } from '@/lib/types';
import { areaName } from '@/components/listing-card';

/** Builds /search with only the filled fields (home spec). */
export function HomeSearch({ areas }: { areas: Area[] }) {
  const t = useTranslations();
  const lang = useLocale() as Lang;
  const router = useRouter();

  function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const f = new FormData(e.currentTarget);
    const q: Record<string, string> = {};
    for (const k of ['area', 'max', 'beds']) {
      const v = toLatinDigits(String(f.get(k) ?? '').trim());
      if (v) q[k] = v;
    }
    // Lets the search page record source "home" for analytics without adding anything to the URL.
    document.cookie = 'bc_src=home; path=/; max-age=10; samesite=lax';
    router.push({ pathname: '/search', query: q });
  }

  return (
    <form className="search-card" onSubmit={submit}>
      <div className="field">
        <label className="f" htmlFor="h-area">{t('f_area')}</label>
        <select id="h-area" name="area" defaultValue="">
          <option value="">{t('all_dhamrai')}</option>
          {areas.map(a => <option key={a.slug} value={a.slug}>{areaName(lang, a)}</option>)}
        </select>
      </div>
      <div className="field">
        <label className="f" htmlFor="h-max">{t('f_max')}</label>
        <input id="h-max" name="max" type="number" inputMode="numeric" min={0} step={500} placeholder={num(lang, 15000)} />
      </div>
      <div className="field">
        <label className="f" htmlFor="h-beds">{t('f_beds')}</label>
        <select id="h-beds" name="beds" defaultValue="">
          <option value="">{t('any')}</option>
          <option value="0">{t('studio')}</option>
          {[1, 2, 3, 4].map(n => <option key={n} value={n}>{num(lang, n)}</option>)}
        </select>
      </div>
      <button className="btn btn-primary" type="submit">{t('search_btn')}</button>
    </form>
  );
}

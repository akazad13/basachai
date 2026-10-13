'use client';

import { useEffect, useRef, useState, useTransition, type ReactNode } from 'react';
import { useLocale, useTranslations } from 'next-intl';
import { usePathname, useRouter } from '@/i18n/navigation';
import { api } from '@/lib/api';
import { money, num, toLatinDigits, type Lang } from '@/lib/format';
import type { Area, SearchPage } from '@/lib/types';
import { ListingCard, areaName } from '@/components/listing-card';
import { readFilters, SORTS, toQuery, type FilterKey, type Filters } from './filters';

const HOME_COOKIE = 'bc_src';

const SORT_LABEL: Record<(typeof SORTS)[number], string> = { confirmed: 'sort_confirmed', newest: 'sort_newest', rent_asc: 'sort_low', rent_desc: 'sort_high' };

/**
 * The results and filters. `filters` come from the server page (not read from the URL here, so the cached area pages
 * stay cacheable). `area` is set on an area page (/rent/dhaka/dhamrai/{area}): the area comes from the path, and
 * changes continue on /search.
 */
export function SearchView({ initial, areas, filters: f, area: pathArea }: { initial: SearchPage | null; areas: Area[]; filters: Filters; area?: string }) {
  const t = useTranslations();
  const lang = useLocale() as Lang;
  const router = useRouter();
  const currentPath = usePathname();
  const pathname = pathArea ? '/search' : currentPath;
  const [pending, startTransition] = useTransition();
  const [sheetOpen, setSheetOpen] = useState(false);
  const [items, setItems] = useState(initial?.items ?? []);
  const [cursor, setCursor] = useState(initial?.nextCursor ?? null);
  const [loadingMore, setLoadingMore] = useState(false);
  const [moreFailed, setMoreFailed] = useState(false);
  const sheetRef = useRef<HTMLElement>(null);
  const filterBtn = useRef<HTMLButtonElement>(null);

  // New server results arrive as props when the URL changes; "show more" pages are local until then.
  useEffect(() => { setItems(initial?.items ?? []); setCursor(initial?.nextCursor ?? null); }, [initial]);

  // "Back to results" on a property page returns here with the same filters.
  const query = toQuery(f);

  // Each search shown is reported from here, not by the server render, which may come from the edge cache.
  // The home form leaves a 10-second cookie so its search is recorded with source "home" (analytics spec).
  useEffect(() => {
    if (!initial) return;
    const key = `bc_searched:${query}`;
    try {
      const at = Number(sessionStorage.getItem(key));
      if (at && Date.now() - at < 30 * 60_000) return;
      sessionStorage.setItem(key, String(Date.now()));
    } catch { /* storage blocked: count it */ }
    const fromHome = document.cookie.split('; ').includes(`${HOME_COOKIE}=home`);
    if (fromHome) document.cookie = `${HOME_COOKIE}=; path=/; max-age=0`;
    void api('/v1/events', {
      method: 'POST', json: { name: 'search_performed', query, resultCount: initial.count, source: fromHome ? 'home' : 'search' },
    }).catch(() => {});
  }, [initial, query]);
  useEffect(() => {
    try { sessionStorage.setItem('bc_last_search', '/search' + (query ? '?' + query : '')); } catch { /* storage blocked */ }
  }, [query]);

  // Filters chosen while the previous change is still loading build on each other, not on the URL the page last rendered.
  const pendingFilters = useRef<Filters | null>(null);
  const currentQuery = useRef(query);
  useEffect(() => {
    currentQuery.current = query;
    // Done once the URL has caught up with the last choice, or nothing is loading any more.
    if (!pending || (pendingFilters.current && toQuery(pendingFilters.current) === query)) pendingFilters.current = null;
  }, [query, pending]);

  // On phones the filters open as a modal sheet: focus moves in, Tab stays inside, and focus returns to "Filters" on close.
  const wasOpen = useRef(false);
  useEffect(() => {
    if (!sheetOpen) {
      if (wasOpen.current) filterBtn.current?.focus();
      wasOpen.current = false;
      return;
    }
    wasOpen.current = true;
    const sheet = sheetRef.current;
    sheet?.querySelector<HTMLElement>('#f-area')?.focus();
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') { setSheetOpen(false); return; }
      if (e.key !== 'Tab' || !sheet) return;
      const focusable = [...sheet.querySelectorAll<HTMLElement>('select, input, button')].filter(el => !el.hasAttribute('disabled') && el.offsetParent !== null);
      const first = focusable[0], last = focusable[focusable.length - 1];
      if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last?.focus(); }
      else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first?.focus(); }
    };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [sheetOpen]);

  function apply(next: Partial<Filters>) {
    const merged = { ...(pendingFilters.current ?? f), ...next };
    pendingFilters.current = merged;
    const q = toQuery(merged);
    // replace, not push: filter tweaks should not fill the back button's history (search spec)
    startTransition(() => router.replace(`${pathname}${q ? '?' + q : ''}`, { scroll: false }));
  }
  const clear = (...keys: FilterKey[]) => apply(Object.fromEntries(keys.map(k => [k, ''])));
  const clearAll = () => {
    const sort = (pendingFilters.current ?? f).sort;
    pendingFilters.current = readFilters(k => (k === 'sort' ? sort : null));
    startTransition(() => router.replace(`${pathname}${sort !== 'confirmed' ? '?sort=' + sort : ''}`, { scroll: false }));
  };

  async function showMore() {
    if (!cursor) return;
    const asked = query;
    setLoadingMore(true);
    try {
      const page = await api<SearchPage>(`/v1/listings?${asked}${asked ? '&' : ''}cursor=${encodeURIComponent(cursor)}`, { headers: { 'X-Search-Source': 'search' } });
      if (currentQuery.current !== asked) return;   // the filters changed meanwhile: this page belongs to the old results
      setItems(prev => [...prev, ...page.items.filter(c => !prev.some(p => p.shortId === c.shortId))]);
      setCursor(page.nextCursor);
    } catch {
      setMoreFailed(true);
    } finally {
      setLoadingMore(false);
    }
  }

  const area = areas.find(a => a.slug === f.area);
  const rentChip = f.min && f.max ? t('rent_range', { a: money(lang, +f.min), b: money(lang, +f.max) })
    : f.min ? t('rent_min_chip', { a: money(lang, +f.min) }) : f.max ? t('rent_max_chip', { b: money(lang, +f.max) }) : null;
  const chips: [FilterKey[], string][] = [];
  if (area) chips.push([['area'], areaName(lang, area)]);
  if (rentChip) chips.push([['min', 'max'], rentChip]);
  if (f.beds) chips.push([['beds'], f.beds === '0' ? t('studio') : t('beds_n', { n: num(lang, +f.beds) })]);
  if (f.baths) chips.push([['baths'], t('baths_n', { n: num(lang, +f.baths) })]);
  if (f.type) chips.push([['type'], t(f.type === 'house' ? 'type_house' : 'type_flat')]);
  if (f.furnish) chips.push([['furnish'], t(`furn_${f.furnish}` as 'furn_semi')]);
  if (f.occ) chips.push([['occ'], t(`occ_${f.occ}` as 'occ_family')]);
  if (f.avail) chips.push([['avail'], t(f.avail === 'now' ? 'avail_now_f' : 'avail_month')]);

  const count = initial?.count ?? 0;
  const select = (key: FilterKey, label: string, options: [string, string][], any = t('any')) => (
    <div className="field">
      <label className="f" htmlFor={`f-${key}`}>{label}</label>
      <select id={`f-${key}`} value={f[key]} onChange={e => apply({ [key]: e.target.value })}>
        <option value="">{any}</option>
        {options.map(([v, text]) => <option key={v} value={v}>{text}</option>)}
      </select>
    </div>
  );
  const rentInput = (key: 'min' | 'max', label: string) => (
    <div className="field">
      <label className="f" htmlFor={`f-${key}`}>{label}</label>
      {/* Applies on change (blur or Enter), not on every keystroke. Re-keyed so it resets when the URL changes. */}
      <input key={f[key]} id={`f-${key}`} type="number" inputMode="numeric" min={0} step={500} defaultValue={f[key]}
        onBlur={e => { const v = toLatinDigits(e.target.value.trim()); if (v !== f[key]) apply({ [key]: v }); }}
        onKeyDown={e => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }} />
    </div>
  );

  let list: ReactNode;
  if (initial === null) {
    list = (
      <div className="empty" role="alert">
        <p>{t('load_error')}</p>
        <div className="actions"><button type="button" className="btn btn-primary btn-sm" onClick={() => router.refresh()}>{t('retry')}</button></div>
      </div>
    );
  } else if (items.length === 0) {
    list = (
      <div className="empty">
        <h2>{t('empty_t')}</h2>
        <p>{t('empty_d')}</p>
        <div className="actions">
          {f.max && <button type="button" className="btn btn-ghost btn-sm" onClick={() => clear('max')}>{t('empty_max')}</button>}
          {f.area && <button type="button" className="btn btn-ghost btn-sm" onClick={() => clear('area')}>{t('empty_area')}</button>}
          {f.beds && <button type="button" className="btn btn-ghost btn-sm" onClick={() => clear('beds')}>{t('empty_beds')}</button>}
          <button type="button" className="btn btn-primary btn-sm" onClick={clearAll}>{t('empty_reset')}</button>
        </div>
      </div>
    );
  } else {
    list = (
      <>
        <div className={`cards list${pending ? ' loading-dim' : ''}`} data-testid="results-list">
          {items.map((c, i) => <ListingCard key={c.shortId} card={c} priority={i < 2} referrer="search" />)}
        </div>
        {cursor && (
          <div className="more">
            {moreFailed && <p className="err" role="alert">{t('load_error')}</p>}
            <button type="button" className="btn btn-ghost" onClick={() => { setMoreFailed(false); void showMore(); }} disabled={loadingMore}>{t('show_more')}</button>
          </div>
        )}
      </>
    );
  }

  return (
    <div className={`results${sheetOpen ? ' filters-open' : ''}`} onClick={e => { if (sheetOpen && e.target === e.currentTarget) setSheetOpen(false); }}>
      <aside className="filters" id="filters" aria-label={t('filters')} ref={sheetRef}
        {...(sheetOpen ? { role: 'dialog', 'aria-modal': true } : {})}>
        <h2>{t('filters')}</h2>
        {select('area', t('f_area'), areas.map(a => [a.slug, areaName(lang, a)]), t('all_dhamrai'))}
        <div className="row2">
          {rentInput('min', t('f_min'))}
          {rentInput('max', t('f_max'))}
        </div>
        {select('beds', t('f_beds'), [['0', t('studio')], ...[1, 2, 3, 4].map(n => [String(n), num(lang, n)] as [string, string])])}
        {select('baths', t('f_baths'), [1, 2, 3].map(n => [String(n), num(lang, n)] as [string, string]))}
        {select('type', t('f_type'), [['flat', t('type_flat')], ['house', t('type_house')]])}
        {select('furnish', t('f_furnish'), [['furnished', t('furn_furnished')], ['semi', t('furn_semi')], ['unfurnished', t('furn_unfurnished')]])}
        {select('occ', t('f_occ'), [['family', t('occ_family')], ['bachelor', t('occ_bachelor')], ['students', t('occ_students')]])}
        {select('avail', t('f_avail'), [['now', t('avail_now_f')], ['month', t('avail_month')]])}
        <button type="button" className="linkish" onClick={clearAll}>{t('clear_all')}</button>
        <div className="sheet-actions">
          <button type="button" className="btn btn-ghost" onClick={clearAll}>{t('clear_all')}</button>
          <button type="button" className="btn btn-primary" onClick={() => setSheetOpen(false)}>{t('show_results')} ({num(lang, count)})</button>
        </div>
      </aside>
      <section aria-labelledby="res-title" inert={sheetOpen || undefined}>
        <h1 id="res-title" style={{ fontSize: 26, marginBottom: 8 }}>{t('homes_in', { a: area ? areaName(lang, area) : t('all_dhamrai') })}</h1>
        <div className="results-head">
          <span className="count" role="status">{pending ? t('loading') : t('results_n', { n: num(lang, count) })}</span>
          <span style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
            <button type="button" ref={filterBtn} className="btn btn-ghost btn-sm filter-btn" aria-controls="filters" aria-expanded={sheetOpen} onClick={() => setSheetOpen(true)}>
              {t('filters')}{chips.length ? ` (${num(lang, chips.length)})` : ''}
            </button>
            <label className="sr" htmlFor="f-sort">{t('sort')}</label>
            <select id="f-sort" value={f.sort} onChange={e => apply({ sort: e.target.value })}>
              {SORTS.map(s => <option key={s} value={s}>{t(SORT_LABEL[s] as 'sort_low')}</option>)}
            </select>
          </span>
        </div>
        {chips.length > 0 && (
          <div className="chips">
            {chips.map(([keys, text]) => (
              <span className="chip" key={keys.join()}>
                {text}
                <button type="button" aria-label={t('remove_filter', { x: text })} onClick={() => clear(...keys)}>×</button>
              </span>
            ))}
          </div>
        )}
        {list}
      </section>
    </div>
  );
}

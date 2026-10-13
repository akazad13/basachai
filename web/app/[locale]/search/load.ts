import { cookies } from 'next/headers';
import { serverGet, VISIT_COOKIE } from '@/lib/api';
import type { Area, Locations, SearchPage } from '@/lib/types';
import { readFilters, toQuery } from './filters';

type SearchParams = Record<string, string | string[] | undefined>;

/** Server-side search for /search and the area pages. Recorded as one search_performed event. */
export async function loadSearch(sp: SearchParams, area?: string): Promise<{ initial: SearchPage | null; areas: Area[] }> {
  const filters = readFilters(k => (k === 'area' && area ? area : typeof sp[k] === 'string' ? (sp[k] as string) : null));
  const jar = await cookies();
  // The home form leaves a 10-second cookie so this search is recorded with source "home" (analytics spec).
  const source = jar.get('bc_src')?.value === 'home' ? 'home' : 'search';
  // The browser's visit id, so this search and a later reveal count as one session ("search to contact").
  const visit = jar.get(VISIT_COOKIE)?.value;
  const headers: Record<string, string> = { 'X-Search-Source': source };
  if (visit && /^[0-9a-f-]{36}$/.test(visit)) headers['X-Session-Id'] = visit;

  const [page, locations] = await Promise.allSettled([
    serverGet<SearchPage>(`/v1/listings?${toQuery(filters)}`, { headers }),
    serverGet<Locations>('/v1/locations'),
  ]);
  return {
    initial: page.status === 'fulfilled' ? page.value : null,
    areas: locations.status === 'fulfilled' ? locations.value.areas : [],
  };
}

import { PAGE_SECONDS, serverGet, TAGS } from '@/lib/api';
import type { Area, Locations, SearchPage } from '@/lib/types';
import { readFilters, toQuery, type Filters } from './filters';

type SearchParams = Record<string, string | string[] | undefined>;

/** The filters in a page's search params (strings only; invalid values dropped). */
export function filtersOf(sp: SearchParams, area?: string): Filters {
  return readFilters(k => (k === 'area' && area ? area : typeof sp[k] === 'string' ? (sp[k] as string) : null));
}

/**
 * Server-side search for /search (fresh each time) and the area pages (cached with the page). It never records an
 * analytics event: the browser reports the search it shows, so cached pages count too (analytics spec).
 */
export async function loadSearch(filters: Filters, cache = false): Promise<{ initial: SearchPage | null; areas: Area[] }> {
  const cached = (tags: string[]) => (cache ? { revalidate: PAGE_SECONDS, tags } : {});
  const [page, locations] = await Promise.allSettled([
    serverGet<SearchPage>(`/v1/listings?${toQuery(filters)}`, { headers: { 'X-Search-Source': 'none' }, ...cached([TAGS.listings]) }),
    serverGet<Locations>('/v1/locations', cached([TAGS.reference])),
  ]);
  // A cached page must not store an outage: throwing keeps the last good page. /search shows the error in place.
  if (cache) for (const r of [page, locations]) if (r.status === 'rejected') throw r.reason;
  return {
    initial: page.status === 'fulfilled' ? page.value : null,
    areas: locations.status === 'fulfilled' ? locations.value.areas : [],
  };
}

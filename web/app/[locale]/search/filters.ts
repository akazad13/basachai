/** Search filters live in the URL so links can be shared (search spec). */
export const FILTER_KEYS = ['area', 'min', 'max', 'beds', 'baths', 'type', 'furnish', 'occ', 'avail', 'q', 'sort'] as const;
export type FilterKey = (typeof FILTER_KEYS)[number];
export type Filters = Record<FilterKey, string>;

export const SORTS = ['confirmed', 'newest', 'rent_asc', 'rent_desc'] as const;

/** Values the API accepts. Anything else in a URL is ignored, as the API ignores it (search spec, "Invalid URL values"). */
const VALID: Partial<Record<FilterKey, (v: string) => boolean>> = {
  area: v => /^[a-z0-9_-]{1,40}$/.test(v),
  min: v => /^\d{1,8}$/.test(v),
  max: v => /^\d{1,8}$/.test(v),
  beds: v => /^(10|\d)$/.test(v),
  baths: v => /^(10|[1-9])$/.test(v),
  type: v => /^[a-z0-9_-]{1,40}$/.test(v),
  furnish: v => ['furnished', 'semi', 'unfurnished'].includes(v),
  occ: v => ['family', 'bachelor', 'students'].includes(v),
  avail: v => v === 'now' || v === 'month',
  q: v => v.length <= 100,
  sort: v => (SORTS as readonly string[]).includes(v),
};

export function readFilters(get: (k: string) => string | null): Filters {
  const f = Object.fromEntries(FILTER_KEYS.map(k => {
    const v = get(k)?.trim() ?? '';
    return [k, v && (VALID[k]?.(v) ?? true) ? v : ''];
  })) as Filters;
  if (!f.sort) f.sort = 'confirmed';
  return f;
}

/** Only the filters in use, in a stable order; the default sort is left out. */
export function toQuery(f: Partial<Filters>): string {
  const q = new URLSearchParams();
  for (const k of FILTER_KEYS) {
    const v = f[k];
    if (v && !(k === 'sort' && v === 'confirmed')) q.set(k, v);
  }
  return q.toString();
}

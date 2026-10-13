/** Numbers, money, dates and phones in the reader's script: Bangla digits in Bangla mode, lakh grouping in both. */

export type Lang = 'bn' | 'en';

const BN_DIGITS = '০১২৩৪৫৬৭৮৯';
const DAY = 86_400_000;

export function num(lang: Lang, n: number): string {
  return new Intl.NumberFormat(lang === 'bn' ? 'bn-BD' : 'en-IN').format(n);
}

export function money(lang: Lang, n: number): string {
  return '৳' + num(lang, n);
}

export function digits(lang: Lang, s: string): string {
  return lang === 'bn' ? s.replace(/[0-9]/g, d => BN_DIGITS[+d]) : s;
}

export function toLatinDigits(s: string): string {
  return s.replace(/[০-৯]/g, d => String(BN_DIGITS.indexOf(d)));
}

/** "3 days ago", "yesterday", "today": whole days, as the prototype shows freshness. */
export function daysAgo(lang: Lang, iso: string, now = Date.now()): string {
  const days = Math.max(0, Math.floor((now - Date.parse(iso)) / DAY));
  return new Intl.RelativeTimeFormat(lang === 'bn' ? 'bn' : 'en', { numeric: 'auto' }).format(-days, 'day');
}

/** For queue and report ages: hours under a day, then days. */
export function ageText(lang: Lang, iso: string, now = Date.now()): string {
  const hours = (now - Date.parse(iso)) / 3_600_000;
  const rtf = new Intl.RelativeTimeFormat(lang === 'bn' ? 'bn' : 'en', { numeric: 'auto' });
  return hours >= 24 ? rtf.format(-Math.floor(hours / 24), 'day') : rtf.format(-Math.max(1, Math.round(hours)), 'hour');
}

export function isFresh(iso: string | null, now = Date.now()): boolean {
  return !!iso && now - Date.parse(iso) <= 7 * DAY;
}

/** "15 November" for a date-only value such as available_from. */
export function dayMonth(lang: Lang, isoDate: string): string {
  return new Intl.DateTimeFormat(lang === 'bn' ? 'bn-BD' : 'en-GB', { day: 'numeric', month: 'long', timeZone: 'UTC' })
    .format(new Date(isoDate + (isoDate.length === 10 ? 'T00:00:00Z' : '')));
}

/** Today's date in Dhaka (UTC+6, no daylight saving), as the API compares availability. */
export function dhakaToday(now = Date.now()): string {
  return new Date(now + 6 * 3_600_000).toISOString().slice(0, 10);
}

export function isAvailableNow(availableFrom: string | null, now = Date.now()): boolean {
  return !availableFrom || availableFrom.slice(0, 10) <= dhakaToday(now);
}

/** +8801900001010 to 01900-001010 (or Bangla digits). tel: links keep Latin digits. */
export function phoneDisplay(lang: Lang, e164: string): string {
  const local = '0' + e164.slice(-10);
  return digits(lang, local.slice(0, 5) + '-' + local.slice(5));
}

/** Mirrors the API rule: Bangla or Latin digits, spaces or dashes, 01X / 8801X / +8801X. */
export function isValidPhone(input: string): boolean {
  const s = toLatinDigits(input.trim()).replace(/[\s-]/g, '');
  return /^(?:\+?880|0)1[3-9]\d{8}$/.test(s);
}

/** 2,400,000,000 to "2.4 GB"; under 1 GB in MB, so a small pilot does not read "0 GB" (Bangla digits in Bangla). */
export function dataSize(lang: Lang, bytes: number): string {
  const [unit, value] = bytes >= 1e9 ? ['gigabyte', bytes / 1e9] : ['megabyte', bytes / 1e6];
  return new Intl.NumberFormat(lang === 'bn' ? 'bn-BD' : 'en-IN', { style: 'unit', unit, maximumFractionDigits: 1 }).format(value);
}

export function percent(lang: Lang, share: number | null, digits = 0): string | null {
  return share == null ? null : new Intl.NumberFormat(lang === 'bn' ? 'bn-BD' : 'en-IN', { style: 'percent', maximumFractionDigits: digits }).format(share);
}

'use client';

import { useLocale, useTranslations } from 'next-intl';
import { daysAgo, isFresh, type Lang } from '@/lib/format';

/** "Confirmed 3 days ago" with a green dot within 7 days, brass after. */
export function Freshness({ at }: { at: string | null }) {
  const t = useTranslations();
  const lang = useLocale() as Lang;
  if (!at) return null;
  return (
    // Relative time can differ by a day between server and browser around midnight.
    <span className={`fresh${isFresh(at) ? '' : ' old'}`} suppressHydrationWarning>
      {t('confirmed', { t: daysAgo(lang, at) })}
    </span>
  );
}

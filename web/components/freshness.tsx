'use client';

import { useEffect, useState } from 'react';
import { useLocale, useTranslations } from 'next-intl';
import { daysAgo, isFresh, type Lang } from '@/lib/format';

/**
 * "Confirmed 3 days ago" with a green dot within 7 days, brass after. Pages are cached at the edge, so the server's
 * "now" can be old: the browser recomputes the text with its own clock once loaded.
 */
export function Freshness({ at }: { at: string | null }) {
  const t = useTranslations();
  const lang = useLocale() as Lang;
  const [now, setNow] = useState<number | null>(null);
  useEffect(() => setNow(Date.now()), []);
  if (!at) return null;
  const time = now ?? undefined;
  return (
    <span className={`fresh${isFresh(at, time) ? '' : ' old'}`} suppressHydrationWarning>
      {t('confirmed', { t: daysAgo(lang, at, time) })}
    </span>
  );
}

'use client';

import { useState, type KeyboardEvent } from 'react';
import { useLocale, useTranslations } from 'next-intl';
import { num, type Lang } from '@/lib/format';
import type { Photo } from '@/lib/types';
import { PhotoTile } from '@/components/photo-tile';

/** Main photo with "Photo i of N" and a thumbnail strip. Left and right arrows move between photos. */
export function Gallery({ photos, hue }: { photos: Photo[]; hue: number }) {
  const t = useTranslations();
  const lang = useLocale() as Lang;
  const [i, setI] = useState(0);
  const n = photos.length;

  function onKey(e: KeyboardEvent) {
    if (n < 2) return;
    if (e.key === 'ArrowRight') { e.preventDefault(); setI(x => (x + 1) % n); }
    if (e.key === 'ArrowLeft') { e.preventDefault(); setI(x => (x - 1 + n) % n); }
  }

  return (
    <div className="gal" onKeyDown={onKey}>
      <div className="main">
        <PhotoTile photo={photos[i] ?? null} hue={hue} size="full" index={i} count={n} priority />
      </div>
      {n > 1 && (
        <div className="thumbs">
          {photos.map((p, k) => (
            <button key={p.id} type="button" aria-current={k === i} aria-label={t('show_photo', { i: num(lang, k + 1) })} onClick={() => setI(k)}>
              <PhotoTile photo={p} hue={hue + k * 17} size="card" />
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

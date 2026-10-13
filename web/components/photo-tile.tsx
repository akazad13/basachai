'use client';

import { useState, type CSSProperties } from 'react';
import { useLocale, useTranslations } from 'next-intl';
import { num, type Lang } from '@/lib/format';
import { photoUrl, type PhotoSize } from '@/lib/photos';
import type { PhotoRef } from '@/lib/types';
import { RoomDrawing } from './icons';

type Props = {
  photo: PhotoRef | null;
  hue: number;
  size: PhotoSize;
  /** "Photo i of n" tag; omitted on thumbnails. */
  index?: number;
  count?: number;
  priority?: boolean;
};

/** A photo over a drawn placeholder. If the photo fails to load the placeholder simply stays. */
export function PhotoTile({ photo, hue, size, index, count, priority }: Props) {
  const t = useTranslations();
  const lang = useLocale() as Lang;
  const [failed, setFailed] = useState(false);
  return (
    <div className="tile" style={{ '--h': hue } as CSSProperties} role="img" aria-label={t('photo_alt')}>
      <RoomDrawing />
      {photo && !failed && (
        // Cloudinary does the resizing; next/image optimisation is off by design.
        // eslint-disable-next-line @next/next/no-img-element
        <img src={photoUrl(photo, size)} alt="" loading={priority ? 'eager' : 'lazy'} decoding="async" onError={() => setFailed(true)} />
      )}
      {count != null && count > 0 && index != null && (
        <span className="tag">{t('photo_n', { i: num(lang, index + 1), n: num(lang, count) })}</span>
      )}
    </div>
  );
}

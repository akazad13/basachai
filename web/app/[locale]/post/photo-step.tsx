'use client';

import { useRef, useState } from 'react';
import { useLocale, useTranslations } from 'next-intl';
import { api, ApiError } from '@/lib/api';
import { num, type Lang } from '@/lib/format';
import type { Photo } from '@/lib/types';
import { PhotoTile } from '@/components/photo-tile';

const MAX_BYTES = 10 * 1024 * 1024;
const MAX_PHOTOS = 20;

/** Step 3: upload one photo per request, reorder (the first is the cover), remove. */
export function PhotoStep({ listingId, photos, onChange, needError, onBusy }: {
  listingId: string; photos: Photo[]; onChange: (p: Photo[]) => void; needError: string; onBusy: (busy: boolean) => void;
}) {
  const t = useTranslations();
  const lang = useLocale() as Lang;
  const input = useRef<HTMLInputElement>(null);
  const [uploading, setUploading] = useState(0);
  const [error, setError] = useState('');
  const saving = useRef<Promise<void>>(Promise.resolve());
  const grid = useRef<HTMLDivElement>(null);
  // The list as last shown, so an upload finishing after a remove or reorder adds to that list rather than an old copy.
  const latest = useRef(photos);
  latest.current = photos;
  const need = Math.max(0, 3 - photos.length);
  const publish = (next: Photo[]) => { latest.current = next; onChange(next); };

  async function upload(files: FileList | null) {
    if (!files?.length) return;
    setError('');
    onBusy(true);   // Next waits until every chosen photo is uploaded
    for (const file of Array.from(files)) {
      if (latest.current.length >= MAX_PHOTOS) { setError(t('photo_limit')); break; }
      // Checked here too, so a large phone photo is refused at once instead of after a slow upload.
      if (file.size > MAX_BYTES) { setError(t('photo_too_large')); continue; }
      setUploading(n => n + 1);
      try {
        const body = new FormData();
        body.append('file', file);
        const photo = await api<Photo>(`/v1/owner/listings/${listingId}/photos`, { method: 'POST', body });
        publish([...latest.current, photo]);
      } catch (e) {
        const code = e instanceof ApiError ? e.code : '';
        setError(code === 'photo_too_large' || code === 'photo_type' || code === 'photo_limit' ? t(code) : t('photo_failed'));
      } finally {
        setUploading(n => n - 1);
      }
    }
    if (input.current) input.current.value = '';
    onBusy(false);
  }

  // Reorders at once on screen, so a second quick press works on the new order; the server copy follows.
  async function move(index: number, by: -1 | 1) {
    const before = latest.current;
    const next = [...before];
    [next[index], next[index + by]] = [next[index + by], next[index]];
    publish(next);
    // Keep focus on the moved photo; at either end its button in that direction is disabled, so use the other one.
    const to = index + by;
    requestAnimationFrame(() => {
      const ops = grid.current?.querySelectorAll<HTMLElement>('[data-testid="photo-item"]')[to]?.querySelectorAll<HTMLButtonElement>('.ops button');
      if (!ops) return;
      const same = by < 0 ? ops[0] : ops[2];
      (same && !same.disabled ? same : by < 0 ? ops[2] : ops[0])?.focus();
    });
    // Saves go one after another, so the last order the owner chose is the one the server keeps.
    saving.current = saving.current.then(() =>
      api<Photo[]>(`/v1/owner/listings/${listingId}/photos`, { method: 'PATCH', json: { order: next.map(p => p.id) } })
        .then(() => undefined, () => { publish(before); setError(t('error_generic')); }));
    await saving.current;
  }

  async function remove(photo: Photo) {
    try {
      await api(`/v1/owner/listings/${listingId}/photos/${photo.id}`, { method: 'DELETE' });
      publish(latest.current.filter(p => p.id !== photo.id));
    } catch { setError(t('error_generic')); }
  }

  return (
    <>
      <h2>{t('step3')}</h2>
      <p className="hint">{t('photos_hint')}</p>
      <div className="photo-grid" ref={grid}>
        {photos.map((p, i) => (
          <div className="photo-item" key={p.id} data-testid="photo-item">
            <PhotoTile photo={p} hue={150 + i * 40} size="card" />
            {i === 0 && <span className="badge b-brass" style={{ justifySelf: 'start' }}>{t('cover')}</span>}
            <div className="ops">
              <button type="button" aria-label={t('move_left')} disabled={i === 0} onClick={() => move(i, -1)}>‹</button>
              <button type="button" aria-label={`${t('remove')} ${num(lang, i + 1)}`} onClick={() => remove(p)}>{t('remove')}</button>
              <button type="button" aria-label={t('move_right')} disabled={i === photos.length - 1} onClick={() => move(i, 1)}>›</button>
            </div>
          </div>
        ))}
        {uploading > 0 && <div className="uploading" role="status">{t('uploading')}</div>}
        {photos.length < MAX_PHOTOS && (
          <label className="add-tile">
            + {t('add_photo')}
            <input ref={input} type="file" accept="image/jpeg,image/png,image/webp" multiple className="sr" onChange={e => upload(e.target.files)} />
          </label>
        )}
      </div>
      {error && <p className="err" role="alert">{error}</p>}
      <p>
        <strong>{t('photos_n', { n: num(lang, photos.length) })}</strong>
        {need > 0 && <> <span className="err" style={{ display: 'inline' }} id="e-photos">{needError || t('need_photos', { n: num(lang, need) })}</span></>}
      </p>
    </>
  );
}

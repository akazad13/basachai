'use client';

import { useEffect, useRef, useState } from 'react';
import { useTranslations } from 'next-intl';
import { Link } from '@/i18n/navigation';
import { api } from '@/lib/api';
import type { Card } from '@/lib/types';
import { ListingCard } from '@/components/listing-card';
import { useSession } from '@/components/session';

/** Saved homes that are still Live (saved-homes spec). */
export default function SavedPage() {
  const t = useTranslations();
  const { me, ready, openSignIn, saved } = useSession();
  const [cards, setCards] = useState<Card[] | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    if (!ready) return;
    if (!me) { openSignIn({ reason: 'signin_why_save' }); return; }
    api<Card[]>('/v1/me/saved').then(setCards, () => setFailed(true));
  }, [ready, me, openSignIn]);

  // Unsaving from this page removes the card at once. Only cards seen saved and then unsaved are hidden, so the
  // page does not depend on the saved-ids list (which loads separately and may fail).
  const seen = useRef(new Set<string>());
  const [removed, setRemoved] = useState<Set<string>>(new Set());
  useEffect(() => {
    setRemoved(prev => {
      const next = new Set(prev);
      for (const id of saved) { seen.current.add(id); next.delete(id); }
      for (const id of seen.current) if (!saved.has(id)) next.add(id);
      return next.size === prev.size && [...next].every(id => prev.has(id)) ? prev : next;
    });
  }, [saved]);
  const visible = cards?.filter(c => !removed.has(c.shortId)) ?? null;

  return (
    <div className="dash">
      <div className="dash-head"><h1 style={{ fontSize: 28 }}>{t('saved_t')}</h1></div>
      {failed && <div className="empty" role="alert"><p>{t('load_error')}</p></div>}
      {visible && visible.length > 0 && (
        <div className="cards grid3">{visible.map(c => <ListingCard key={c.shortId} card={c} referrer="saved" />)}</div>
      )}
      {visible && visible.length === 0 && (
        <div className="empty">
          <p>{t('saved_empty')}</p>
          <div className="actions"><Link className="btn btn-primary" href="/search">{t('nav_find')}</Link></div>
        </div>
      )}
    </div>
  );
}

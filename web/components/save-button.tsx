'use client';

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { api } from '@/lib/api';
import { useSession } from './session';
import { useToast } from './toast';

/** Save toggle. Signed out, it opens sign-in and saves once signed in, without a second press (AUTH-3, SAVE-3). */
export function SaveButton({ shortId }: { shortId: string }) {
  const t = useTranslations();
  const toast = useToast();
  const { me, saved, setSaved, openSignIn } = useSession();
  const [busy, setBusy] = useState(false);
  const on = !!me && saved.has(shortId);

  async function save(next: boolean) {
    setBusy(true);
    try {
      await api(`/v1/me/saved/${shortId}`, { method: next ? 'PUT' : 'DELETE' });
      setSaved(shortId, next);
      toast(t(next ? 'toast_saved' : 'toast_unsaved'));
    } catch {
      toast(t('error_generic'));
    } finally {
      setBusy(false);
    }
  }

  return (
    <button
      type="button"
      className="save-btn"
      aria-pressed={on}
      disabled={busy}
      onClick={() => (me ? save(!on) : openSignIn({ reason: 'signin_why_save', then: () => save(true) }))}
    >
      <span aria-hidden="true">{on ? '★' : '☆'}</span> {t(on ? 'saved' : 'save')}
    </button>
  );
}

'use client';

import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { api, ensureCsrf, sessionId } from '@/lib/api';
import type { Me } from '@/lib/types';
import { SignInDialog } from './sign-in-dialog';

type SignInRequest = { reason?: 'signin_why_save' | 'signin_why_post'; then?: (me: Me) => void | Promise<void> };

type Session = {
  me: Me | null;
  /** False until the first /v1/me answer, so pages do not flash a signed-out state. */
  ready: boolean;
  saved: Set<string>;
  attention: number;
  refresh: () => Promise<Me | null>;
  refreshAttention: () => Promise<void>;
  setSaved: (shortId: string, on: boolean) => void;
  openSignIn: (request?: SignInRequest) => void;
  signOut: () => Promise<void>;
  isOwner: boolean;
  isStaff: boolean;
};

const SessionContext = createContext<Session | null>(null);

export function SessionProvider({ children }: { children: ReactNode }) {
  const [me, setMe] = useState<Me | null>(null);
  const [ready, setReady] = useState(false);
  const [saved, setSavedIds] = useState<Set<string>>(new Set());
  const [attention, setAttention] = useState(0);
  const [request, setRequest] = useState<SignInRequest | null>(null);

  const refreshAttention = useCallback(async () => {
    try { setAttention((await api<{ count: number }>('/v1/me/attention')).count); } catch { /* badge is optional */ }
  }, []);

  const refresh = useCallback(async () => {
    let next: Me | null = null;
    try { next = (await api<{ me: Me | null }>('/v1/auth/session')).me; }
    catch (e) { console.warn('Could not load the session', e); }
    setMe(next);
    setReady(true);
    if (next) {
      try { await ensureCsrf(); } catch { /* api() retries with a fresh token */ }
      try { setSavedIds(new Set(await api<string[]>('/v1/me/saved/ids'))); } catch { /* saved state is optional */ }
      if (next.roles.includes('owner')) await refreshAttention();
    } else {
      setSavedIds(new Set());
      setAttention(0);
    }
    return next;
  }, [refreshAttention]);

  useEffect(() => { sessionId(); void refresh(); }, [refresh]);

  const setSaved = useCallback((shortId: string, on: boolean) => {
    setSavedIds(prev => {
      const s = new Set(prev);
      if (on) s.add(shortId); else s.delete(shortId);
      return s;
    });
  }, []);

  const signOut = useCallback(async () => {
    await api('/v1/auth/logout', { method: 'POST' });
    await refresh();
  }, [refresh]);

  // Stable, so pages that open the dialog from an effect do not re-run it on every session change.
  const openSignIn = useCallback((r?: SignInRequest) => setRequest(r ?? {}), []);

  const value = useMemo<Session>(() => ({
    me, ready, saved, attention, refresh, refreshAttention, setSaved, signOut, openSignIn,
    isOwner: !!me?.roles.includes('owner'),
    isStaff: !!me && (me.roles.includes('moderator') || me.roles.includes('admin')),
  }), [me, ready, saved, attention, refresh, refreshAttention, setSaved, signOut, openSignIn]);

  return (
    <SessionContext.Provider value={value}>
      {children}
      {request && (
        <SignInDialog
          reason={request.reason}
          onClose={() => setRequest(null)}
          onSignedIn={async () => {
            const current = await refresh();
            setRequest(null);
            if (current && request.then) await request.then(current);
          }}
        />
      )}
    </SessionContext.Provider>
  );
}

export function useSession(): Session {
  const s = useContext(SessionContext);
  if (!s) throw new Error('useSession must be used inside SessionProvider');
  return s;
}

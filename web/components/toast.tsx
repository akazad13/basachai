'use client';

import { createContext, useCallback, useContext, useRef, useState, type ReactNode } from 'react';

const ToastContext = createContext<(message: string) => void>(() => {});

/** One short confirmation at a time, announced politely. The live region is always present so screen readers notice changes. */
export function ToastProvider({ children }: { children: ReactNode }) {
  const [message, setMessage] = useState('');
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  const show = useCallback((m: string) => {
    setMessage(m);
    clearTimeout(timer.current);
    timer.current = setTimeout(() => setMessage(''), 3200);
  }, []);
  return (
    <ToastContext.Provider value={show}>
      {children}
      {/* The outer live region is always mounted, so the message added inside it is announced. The inner status
          is not a live region itself (aria-live off), so the message is read once. */}
      <div aria-live="polite" aria-atomic="true">
        {message && <div id="toast" className="show" role="status" aria-live="off">{message}</div>}
      </div>
    </ToastContext.Provider>
  );
}

export const useToast = () => useContext(ToastContext);

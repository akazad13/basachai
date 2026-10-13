/** Inline icons from the prototype. Decorative: every one is aria-hidden next to visible text. */

const nav = { className: 'nav-icon', width: 22, height: 22, viewBox: '0 0 24 24', 'aria-hidden': true } as const;
const stroke = { fill: 'none', stroke: 'currentColor', strokeWidth: 2 } as const;

export const RoofIcon = () => (
  <svg width="26" height="20" viewBox="0 0 26 20" aria-hidden="true">
    <path d="M2 11 L13 2 L24 11" {...stroke} strokeWidth={2.4} strokeLinecap="round" strokeLinejoin="round" />
    <path d="M6 10v8h14v-8" {...stroke} strokeWidth={2.4} strokeLinejoin="round" />
  </svg>
);
export const SearchIcon = () => (
  <svg {...nav}><circle cx="11" cy="11" r="6.5" {...stroke} /><path d="M16 16l5 5" stroke="currentColor" strokeWidth="2" strokeLinecap="round" /></svg>
);
export const HeartIcon = () => (
  <svg {...nav}><path d="M12 20s-7-4.4-7-10a4 4 0 0 1 7-2.6A4 4 0 0 1 19 10c0 5.6-7 10-7 10z" {...stroke} strokeLinejoin="round" /></svg>
);
export const PlusIcon = () => (
  <svg {...nav}><path d="M12 5v14M5 12h14" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" /></svg>
);
export const ListIcon = () => (
  <svg {...nav}><path d="M8 6h12M8 12h12M8 18h12M4 6h.01M4 12h.01M4 18h.01" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" /></svg>
);
export const ShieldIcon = () => (
  <svg {...nav}><path d="M12 3l7 3v6c0 4.5-3 7.5-7 9-4-1.5-7-4.5-7-9V6z" {...stroke} strokeLinejoin="round" /></svg>
);
export const UserIcon = () => (
  <svg {...nav}><circle cx="12" cy="8" r="4" {...stroke} /><path d="M4 21c1-4 4.5-6 8-6s7 2 8 6" {...stroke} strokeLinecap="round" /></svg>
);
export const EyeIcon = () => (
  <svg width="28" height="28" viewBox="0 0 24 24" aria-hidden="true">
    <path d="M2 12s3.6-6 10-6 10 6 10 6-3.6 6-10 6S2 12 2 12z" {...stroke} /><circle cx="12" cy="12" r="3" {...stroke} />
  </svg>
);
export const CheckIcon = () => (
  <svg width="30" height="30" viewBox="0 0 24 24" aria-hidden="true">
    <path d="M5 12.5l4.5 4.5L19 7.5" {...stroke} strokeWidth={2.4} strokeLinecap="round" strokeLinejoin="round" />
  </svg>
);
/** The drawn room shown under every photo while it loads, or instead of it when offline. */
export const RoomDrawing = () => (
  <svg viewBox="0 0 120 80" aria-hidden="true">
    <rect x="38" y="12" width="44" height="34" rx="2" /><path d="M60 12v34M38 29h44" /><path d="M0 62h120" />
    <path d="M14 62V40h14v22" /><path d="M92 62v-9h18v9" />
  </svg>
);

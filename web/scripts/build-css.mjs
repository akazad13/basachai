// One-off: turns the prototype stylesheet into app/globals.css for the real app.
// Run from web/: node scripts/build-css.mjs
import fs from 'node:fs';

let css = fs.readFileSync('app/prototype.css', 'utf8');
const swaps = [
  ['--display: "Baloo Da 2", "Noto Sans Bengali", system-ui, sans-serif;', '--display: var(--font-baloo), var(--font-noto), system-ui, sans-serif;'],
  ['--sans: "Noto Sans Bengali", system-ui, -apple-system, "Segoe UI", sans-serif;', '--sans: var(--font-noto), system-ui, -apple-system, "Segoe UI", sans-serif;'],
  ['html[lang="en"] { --sans: "Nunito", "Noto Sans Bengali", system-ui, -apple-system, "Segoe UI", sans-serif; }',
   'html[lang="en"] { --sans: var(--font-nunito), var(--font-noto), system-ui, -apple-system, "Segoe UI", sans-serif; }'],
  // The filter sheet opens by a class on the results wrapper instead of on <body>.
  ['body.filters-open .filters { display: block; }', '.results.filters-open .filters { display: block; }'],
  ['body.filters-open::after {', '.results.filters-open::after {'],
];
for (const [from, to] of swaps) {
  if (!css.includes(from)) throw new Error('Not found: ' + from);
  css = css.replace(from, to);
}
// The prototype-only role switcher.
css = css.split('\n').filter(l => !/^\s*\.(proto|seg)\b/.test(l)).join('\n');

css = `/* BasaChai global styles. Source: BasaChai-Prototype.html (design-system spec). Light theme only (D19). */
${css}
/* ---------- Additions for the real app ---------- */
.skip { position: absolute; left: 8px; top: -60px; z-index: 200; background: var(--surface); padding: 8px 12px; border-radius: 8px; border: 2px solid var(--brass); }
.skip:focus { top: 8px; }
.banner { border-radius: 12px; padding: 10px 14px; margin: 16px 0 0; border: 1px solid var(--brass); background: var(--brass-tint); }
.banner.red { border-color: var(--red); background: var(--red-tint); }
.more { display: flex; justify-content: center; margin: 20px 0 8px; }
.loading-dim { opacity: .55; transition: opacity .15s; }
.phone-big { font-family: var(--display); font-size: 28px; font-weight: 700; letter-spacing: .02em; }
.form-error { color: var(--red); margin: 8px 0 0; }
.tabs { display: flex; flex-wrap: wrap; gap: 6px; margin: 0 0 20px; }
.tabs a { padding: 6px 14px; border-radius: 999px; border: 1px solid var(--line); background: var(--surface); text-decoration: none; color: var(--ink); }
.tabs a[aria-current="page"] { background: var(--green); color: var(--on-green); border-color: var(--green); }
.table-wrap { overflow-x: auto; background: var(--surface); border: 1px solid var(--line); border-radius: 14px; }
table.data { width: 100%; border-collapse: collapse; font-size: 14.5px; }
table.data th, table.data td { text-align: left; padding: 8px 12px; border-bottom: 1px solid var(--line); vertical-align: top; }
table.data th { font-weight: 600; color: var(--muted); }
.prose { max-width: 70ch; padding: 24px 0 40px; }
.prose li { margin-bottom: 8px; }
.key-box { font-family: ui-monospace, monospace; background: var(--paper); border: 1px solid var(--line); padding: 8px 12px; border-radius: 8px; word-break: break-all; }
.diff { display: grid; grid-template-columns: auto 1fr 1fr; gap: 2px 12px; font-size: 14px; margin-top: 6px; }
`;
fs.writeFileSync('app/globals.css', css);
fs.unlinkSync('app/prototype.css');
console.log('globals.css', css.length, 'bytes');

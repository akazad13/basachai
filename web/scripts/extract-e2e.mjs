// Copies the Playwright code blocks from the specs into web/e2e, so the tests run exactly as specified.
// Every block that starts with a "// web/e2e/..." comment becomes that file. Run from web/: node scripts/extract-e2e.mjs
import fs from 'node:fs';
import path from 'node:path';

const specs = path.resolve('../docs/specs');
const files = [];
const walk = dir => {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) walk(p); else if (p.endsWith('.md')) files.push(p);
  }
};
walk(specs);

const written = [];
for (const file of files) {
  const md = fs.readFileSync(file, 'utf8');
  for (const m of md.matchAll(/```ts\n(\/\/ web\/(e2e\/[^\s]+\.ts)[^\n]*\n[\s\S]*?)```/g)) {
    const target = m[2];
    const body = `// Extracted from specs/${path.relative(specs, file).replaceAll('\\', '/')} by scripts/extract-e2e.mjs. Edit the spec, then re-extract.\n${m[1]}`;
    fs.mkdirSync(path.dirname(target), { recursive: true });
    if (written.includes(target)) fs.appendFileSync(target, '\n' + m[1]);
    else fs.writeFileSync(target, body);
    written.push(target);
  }
}
console.log([...new Set(written)].join('\n'));

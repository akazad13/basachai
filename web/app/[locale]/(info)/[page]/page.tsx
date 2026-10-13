import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { getTranslations, setRequestLocale } from 'next-intl/server';
import { routing } from '@/i18n/routing';

/** Footer pages. Terms and privacy are marked as drafts until a lawyer reviews them (launch checklist item 13). */
const PAGES = {
  about: { title: 'about_t2', body: ['about_d'], list: [] },
  'how-it-works': { title: 'how_t', body: [], list: ['how1', 'how2', 'how3'] },
  safety: { title: 'safety_page_t', body: ['safety_d'], list: ['safety_1', 'safety_2', 'safety_3', 'safety_4', 'safety_5'] },
  support: { title: 'support_t', body: ['support_d', 'support_channel'], list: [] },
  terms: { title: 'terms_t', body: ['draft_legal'], list: ['terms_1', 'terms_2', 'terms_3', 'terms_4'] },
  privacy: { title: 'privacy_t', body: ['draft_legal'], list: ['privacy_1', 'privacy_2', 'privacy_3', 'privacy_4', 'privacy_5'] },
} as const;
type PageKey = keyof typeof PAGES;

export const dynamicParams = false;
export function generateStaticParams() {
  return routing.locales.flatMap(locale => Object.keys(PAGES).map(page => ({ locale, page })));
}

type Params = { params: Promise<{ locale: string; page: string }> };

export async function generateMetadata({ params }: Params): Promise<Metadata> {
  const { locale, page } = await params;
  if (!(page in PAGES)) return {};
  const t = await getTranslations({ locale });
  return { title: t(PAGES[page as PageKey].title) };
}

export default async function InfoPage({ params }: Params) {
  const { locale, page } = await params;
  if (!(page in PAGES)) notFound();
  setRequestLocale(locale);
  const t = await getTranslations();
  const p = PAGES[page as PageKey];
  return (
    <article className="prose">
      <h1 style={{ fontSize: 30 }}>{t(p.title)}</h1>
      {p.body.map(k => <p key={k} className={k === 'draft_legal' ? 'banner' : undefined}>{t(k)}</p>)}
      {p.list.length > 0 && (
        page === 'how-it-works'
          ? <ol className="steps">{p.list.map(k => <li key={k}><h2 style={{ fontSize: 20 }}>{t(`${k}_t` as 'how1_t')}</h2><p>{t(`${k}_d` as 'how1_d')}</p></li>)}</ol>
          : <ul>{p.list.map(k => <li key={k}>{t(k as 'safety_1')}</li>)}</ul>
      )}
    </article>
  );
}

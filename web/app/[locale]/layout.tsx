import type { Metadata, Viewport } from 'next';
import { Suspense, type ReactNode } from 'react';
import { notFound } from 'next/navigation';
import { Baloo_Da_2, Noto_Sans_Bengali, Nunito } from 'next/font/google';
import { NextIntlClientProvider, hasLocale } from 'next-intl';
import { getTranslations, setRequestLocale } from 'next-intl/server';
import { routing } from '@/i18n/routing';
import { Footer } from '@/components/footer';
import { Header } from '@/components/header';
import { SessionProvider } from '@/components/session';
import { ToastProvider } from '@/components/toast';
import '../globals.css';

// next/font self-hosts the fonts, so pages make no requests to Google (design-system spec: self-host before launch).
const baloo = Baloo_Da_2({ subsets: ['bengali', 'latin'], weight: ['500', '600', '700', '800'], variable: '--font-baloo', display: 'swap' });
const noto = Noto_Sans_Bengali({ subsets: ['bengali', 'latin'], weight: ['400', '500', '600', '700'], variable: '--font-noto', display: 'swap' });
const nunito = Nunito({ subsets: ['latin'], weight: ['400', '600', '700'], variable: '--font-nunito', display: 'swap' });

export const viewport: Viewport = { colorScheme: 'light', themeColor: '#0F5E50' };

export async function generateMetadata({ params }: { params: Promise<{ locale: string }> }): Promise<Metadata> {
  const { locale } = await params;
  const t = await getTranslations({ locale });
  return {
    metadataBase: new URL(process.env.NEXT_PUBLIC_SITE_URL ?? 'http://localhost:3000'),
    title: { default: `${t('brand')} | BasaChai`, template: '%s | BasaChai' },
    description: t('hero_sub'),
    other: { 'color-scheme': 'light only' },
  };
}

export function generateStaticParams() {
  return routing.locales.map(locale => ({ locale }));
}

export default async function LocaleLayout({ children, params }: { children: ReactNode; params: Promise<{ locale: string }> }) {
  const { locale } = await params;
  if (!hasLocale(routing.locales, locale)) notFound();
  setRequestLocale(locale);
  const t = await getTranslations();
  return (
    <html lang={locale} className={`${baloo.variable} ${noto.variable} ${nunito.variable}`}>
      <body>
        <NextIntlClientProvider>
          <ToastProvider>
            <SessionProvider>
              <a className="skip" href="#main">{t('skip')}</a>
              <Suspense fallback={<header className="site" />}><Header /></Suspense>
              <main id="main" className="wrap">{children}</main>
              <Footer />
            </SessionProvider>
          </ToastProvider>
        </NextIntlClientProvider>
      </body>
    </html>
  );
}

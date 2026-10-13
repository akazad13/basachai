import { NextResponse, type NextRequest } from 'next/server';
import createMiddleware from 'next-intl/middleware';
import { routing } from './i18n/routing';

const intl = createMiddleware(routing);
const AREA_PAGE = /^\/(bn|en)\/rent\/dhaka\/dhamrai\/([a-z0-9_-]+)\/?$/;

export default function middleware(request: NextRequest) {
  // Area pages are cached without their query, so a filtered area URL continues on /search with the same filters.
  const area = AREA_PAGE.exec(request.nextUrl.pathname);
  if (area && request.nextUrl.search) {
    const url = request.nextUrl.clone();
    url.pathname = `/${area[1]}/search`;
    url.searchParams.set('area', area[2]);
    return NextResponse.redirect(url);
  }
  return intl(request);
}

export const config = {
  // Everything except API routes, Next.js internals and files with an extension.
  matcher: ['/((?!api|_next|_vercel|.*\\..*).*)'],
};

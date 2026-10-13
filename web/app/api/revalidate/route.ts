import { createHash, timingSafeEqual } from 'node:crypto';
import { revalidateTag } from 'next/cache';
import { TAGS } from '@/lib/api';

const digest = (s: string) => createHash('sha256').update(s).digest();

/**
 * Called by the API after a change a visitor could see: drops the cached pages of those listings (and the lists that
 * show them), or every cached page. Signed with REVALIDATE_SECRET, the same value as the API's Web:RevalidateSecret.
 */
export async function POST(request: Request) {
  const secret = process.env.REVALIDATE_SECRET ?? '';
  const given = request.headers.get('x-revalidate-secret') ?? '';
  if (secret.length < 16 || !timingSafeEqual(digest(given), digest(secret))) return new Response(null, { status: 401 });

  let body: unknown;
  try { body = await request.json(); } catch { return new Response(null, { status: 400 }); }
  if (typeof body !== 'object' || body === null) return new Response(null, { status: 400 });
  const { listings: raw, all } = body as { listings?: unknown; all?: unknown };
  const listings = Array.isArray(raw) ? raw.filter((id): id is string => typeof id === 'string' && /^[A-Za-z0-9]{1,16}$/.test(id)) : [];

  if (all === true) {
    // Every page built from API data. Not revalidatePath('/', 'layout'): that also drops the static pages, and ones
    // with dynamicParams = false (the footer pages) then cannot regenerate and turn 404.
    revalidateTag(TAGS.pages);
  } else {
    for (const id of listings) revalidateTag(TAGS.listing(id));
    if (listings.length) revalidateTag(TAGS.listings);   // home and area pages list them
  }
  return Response.json({ purged: all === true ? 'all' : listings });
}

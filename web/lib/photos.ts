import type { PhotoRef } from './types';

/** Named transformations on Cloudinary (strict transformations are on, so only these sizes exist). */
export type PhotoSize = 'card' | 'detail' | 'full';
const WIDTH: Record<PhotoSize, number> = { card: 320, detail: 768, full: 1280 };

const CLOUD = process.env.NEXT_PUBLIC_CLOUDINARY_CLOUD ?? '';

export function photoUrl(photo: PhotoRef, size: PhotoSize): string {
  // The pilot test dataset points at free-licence demo photos instead of Cloudinary uploads.
  if (photo.publicId.startsWith('seed/')) {
    const w = WIDTH[size] * (size === 'card' ? 2 : 1);
    return `https://images.unsplash.com/photo-${photo.publicId.slice(5)}?w=${w}&q=70&auto=format&fit=crop`;
  }
  // Development and test runs without Cloudinary keep uploads on the API's disk.
  if (photo.publicId.startsWith('local/'))
    return `${process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5080'}/v1/dev-photos/${photo.publicId.slice(6)}`;
  return `https://res.cloudinary.com/${CLOUD}/image/upload/t_${size}/v${photo.version}/${photo.publicId}`;
}

/** A stable hue per listing for the drawn placeholder underneath each photo. */
export function hueFor(id: string): number {
  let h = 0;
  for (const c of id) h = (h * 31 + c.charCodeAt(0)) % 360;
  return h;
}

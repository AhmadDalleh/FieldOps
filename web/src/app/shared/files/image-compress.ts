/** Photos are scaled so the longer side is at most this many pixels before upload (US-TAPP-06 AC1). */
export const MAX_PHOTO_SIDE = 1600;
export const MAX_UPLOAD_BYTES = 10 * 1024 * 1024;
export const PHOTO_TYPES = ['image/jpeg', 'image/png', 'image/webp'];

/** The size that fits within `max` on the longer side, keeping the aspect ratio and never enlarging. */
export function fitWithin(width: number, height: number, max = MAX_PHOTO_SIDE): { width: number; height: number } {
  const scale = Math.min(1, max / Math.max(width, height));
  return { width: Math.round(width * scale), height: Math.round(height * scale) };
}

/** Re-encodes a photo as a JPEG no larger than 1600px; falls back to the original when the browser cannot decode it. */
export async function compressPhoto(file: File, quality = 0.85): Promise<Blob> {
  let bitmap: ImageBitmap;
  try {
    bitmap = await createImageBitmap(file);
  } catch {
    return file;
  }
  const size = fitWithin(bitmap.width, bitmap.height);
  const canvas = document.createElement('canvas');
  canvas.width = size.width;
  canvas.height = size.height;
  canvas.getContext('2d')!.drawImage(bitmap, 0, 0, size.width, size.height);
  bitmap.close();
  const blob = await new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, 'image/jpeg', quality));
  return blob ?? file;
}

/** Swaps the extension for `.jpg` once a photo has been re-encoded. */
export function jpegName(name: string): string {
  const base = name.replace(/\.[^.]*$/, '') || 'photo';
  return `${base}.jpg`;
}

/** Hands a downloaded file to the browser as a normal download. */
export function saveBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

/** The file name from a Content-Disposition header, or the fallback. */
export function fileNameFrom(disposition: string | null, fallback: string): string {
  const star = disposition?.match(/filename\*=UTF-8''([^;]+)/i);
  if (star) return decodeURIComponent(star[1]);
  const plain = disposition?.match(/filename="?([^";]+)"?/i);
  return plain ? plain[1] : fallback;
}

import { HttpResponse } from '@angular/common/http';

/** Extract a filename from a Content-Disposition header, if present. */
export function filenameFromDisposition(
  disposition: string | null,
): string | null {
  if (!disposition) {
    return null;
  }
  const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition);
  return match ? decodeURIComponent(match[1]) : null;
}

/** Trigger a browser download for a blob with the given filename. */
export function triggerDownload(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}

/**
 * Trigger a browser download from a blob HTTP response, honouring the
 * server-supplied filename (Content-Disposition) and falling back otherwise.
 * Returns `false` when the response carried no body.
 */
export function downloadBlobResponse(
  res: HttpResponse<Blob>,
  fallbackName: string,
): boolean {
  const blob = res.body;
  if (!blob) {
    return false;
  }
  const filename =
    filenameFromDisposition(res.headers.get('Content-Disposition')) ??
    fallbackName;
  triggerDownload(blob, filename);
  return true;
}

import { HttpErrorResponse } from '@angular/common/http';

/** Reads the title of an RFC 9457 ProblemDetails response, or falls back to a generic message. */
export function problemMessage(error: unknown, fallback = 'Something went wrong. Please try again.'): string {
  if (error instanceof HttpErrorResponse && typeof error.error?.title === 'string') {
    const details = error.error.errors as Record<string, string[]> | undefined;
    const first = details ? Object.values(details).flat()[0] : undefined;
    return first ?? error.error.title;
  }
  return fallback;
}

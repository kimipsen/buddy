import { HttpErrorResponse } from '@angular/common/http';

// Accepting a group or guardian invite answers 403 `email_not_verified` when the invite was sent
// to the caller's own address but they haven't verified it yet; a plain 403 means the invite was
// sent to a different address.
export function isEmailNotVerified(error: unknown): boolean {
  return (
    error instanceof HttpErrorResponse &&
    error.status === 403 &&
    (error.error as { code?: unknown } | null)?.code === 'email_not_verified'
  );
}

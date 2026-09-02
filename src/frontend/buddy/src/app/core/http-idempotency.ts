import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, retry, timer } from 'rxjs';

const RETRY_ATTEMPTS = 2;
const RETRY_BASE_DELAY_MS = 300;

// A POST whose response never arrives (dropped connection, timeout, backgrounded tab) is
// indistinguishable from one that never reached the server -- the backend may already have
// created the meal/group/child/etc. Retrying blindly would risk a duplicate, so this is only
// safe paired with the backend's Idempotency-Key support (buddy.Common.Idempotency): every
// attempt for one call carries the same key, so a retry replays the first attempt's response
// instead of repeating its side effects (including a second invite email, etc.).
//
// Only for POST: DELETE/PUT/PATCH endpoints in this API are already idempotent by construction
// (see docs/backend/http-status-codes.md), so retrying them plainly is already safe.
export function postIdempotent<T>(http: HttpClient, url: string, body: unknown): Observable<T> {
  const key = crypto.randomUUID();

  return http.post<T>(url, body, { headers: { 'Idempotency-Key': key } }).pipe(
    retry({
      count: RETRY_ATTEMPTS,
      delay: (error: unknown, retryCount: number) => {
        if (!isTransient(error)) {
          throw error;
        }

        return timer(RETRY_BASE_DELAY_MS * retryCount);
      }
    })
  );
}

// Status 0: the request never reached the server, or its response never came back at all
// (offline, DNS failure, connection reset, timeout) -- exactly the "did this actually happen?"
// case the Idempotency-Key exists for. 5xx is the server's own signal that the attempt didn't
// complete cleanly. Anything else (4xx) is a real rejection of this specific request -- retrying
// it verbatim would just fail again the same way, so it's rethrown as-is instead.
function isTransient(error: unknown): boolean {
  return error instanceof HttpErrorResponse && (error.status === 0 || error.status >= 500);
}

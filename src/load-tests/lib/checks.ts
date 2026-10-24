import { check } from 'k6';
import type { RefinedResponse, ResponseType } from 'k6/http';
import { Counter } from 'k6/metrics';

// Any 5xx fails the run in every profile, breakpoint included: a 5xx means the API broke, never
// that it was busy -- busy is 429 (load-testing-k6.md, "Thresholds").
export const serverErrors = new Counter('server_errors');

// The first few failures per VU go to the log with their status and body, so a failed run says
// why (a 429, a 401 after a token problem, a validation error) without flooding the output.
let logged = 0;

export function expectOk(
  response: RefinedResponse<ResponseType | undefined>,
  name: string,
): boolean {
  if (response.status >= 500 || response.status === 0) {
    serverErrors.add(1, { name });
  }

  const ok = check(response, {
    [`${name} is 2xx/304`]: (r) => (r.status >= 200 && r.status < 300) || r.status === 304,
  });

  if (!ok && logged < 5) {
    logged++;
    const body = typeof response.body === 'string' ? response.body.slice(0, 200) : '';
    console.warn(`${name} -> ${response.status} ${response.error || ''} ${body}`);
  }

  return ok;
}

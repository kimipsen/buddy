import http, { type RefinedResponse, type ResponseType } from 'k6/http';

import { bearer, type TokenSet } from './auth.ts';
import { expectOk } from './checks.ts';
import { config } from './config.ts';
import { pathOf, type Body, type Params, type Route, type RouteWith } from './contract.ts';

// A typed wrapper over k6/http that sends what the real frontend sends, so the API sees the same
// workload as in production:
// - Authorization from the per-VU token cache (auth.ts), refreshed before it expires;
// - Idempotency-Key on every POST, as postIdempotent does (core/idempotency.ts);
// - If-None-Match with the last ETag seen for a URL, as the browser's HTTP cache does
//   (conditional-get-etags.md); a 304 counts as a success.
// Every request is tagged with its route template (name) and kind (read/write/ical), which is what
// the thresholds and reports group by -- never the raw URL, or each id becomes its own series.

// 304 Not Modified is a success, not a failure, for http_req_failed.
http.setResponseCallback(http.expectedStatuses({ min: 200, max: 399 }));

type Response = RefinedResponse<ResponseType | undefined>;
type WriteMethod = 'post' | 'put' | 'patch' | 'delete';

export interface Read {
  route: RouteWith<'get'>;
  params?: Params;
  query?: string;
}

// Per VU (each VU has its own module instance): "<user> <url>" -> last ETag.
const etags = new Map<string, string>();

function conditional(key: string): Record<string, string> {
  const etag = etags.get(key);
  return etag ? { 'If-None-Match': etag } : {};
}

function remember(key: string, response: Response): void {
  const etag = response.headers['Etag'];

  if (etag) {
    etags.set(key, etag);
  }
}

export class Api {
  constructor(
    readonly username: string,
    private readonly seeded?: TokenSet,
  ) {}

  private headers(extra: Record<string, string> = {}): Record<string, string> {
    return { Authorization: `Bearer ${bearer(this.username, this.seeded)}`, ...extra };
  }

  get(route: RouteWith<'get'>, params?: Params, query?: string): Response {
    return this.getAll([{ route, params, query }], 1)[0];
  }

  // At most `concurrency` requests in flight, like mapWithConcurrency(…, PER_ITEM_REQUEST_CONCURRENCY)
  // in the frontend (core/map-with-concurrency.ts): the dashboard's per-child fan-out.
  getAll(reads: Read[], concurrency = 4): Response[] {
    const responses: Response[] = [];

    for (let i = 0; i < reads.length; i += concurrency) {
      const chunk = reads.slice(i, i + concurrency).map(({ route, params, query }) => {
        const url = `${config.baseUrl}${pathOf(route, params, query)}`;
        return { route, url, key: `${this.username} ${url}` };
      });
      const batch = http.batch(
        chunk.map(({ route, url, key }) => ({
          method: 'GET',
          url,
          params: { headers: this.headers(conditional(key)), tags: { name: route, kind: 'read' } },
        })),
      ) as Response[];

      batch.forEach((response, j) => {
        remember(chunk[j].key, response);
        expectOk(response, chunk[j].route);
      });
      responses.push(...batch);
    }

    return responses;
  }

  send<M extends WriteMethod, P extends RouteWith<M>>(
    method: M,
    route: P,
    params: Params,
    body?: Body<P, M>,
    query?: string,
  ): Response {
    const response = http.request(
      method.toUpperCase(),
      `${config.baseUrl}${pathOf(route, params, query)}`,
      body === undefined ? null : JSON.stringify(body),
      {
        headers: this.headers({
          'Content-Type': 'application/json',
          ...(method === 'post' ? { 'Idempotency-Key': crypto.randomUUID() } : {}),
        }),
        tags: { name: route, kind: 'write' },
      },
    );
    expectOk(response, route);
    return response;
  }

  // Methods, not generic arrow-function fields: k6 2.3's TypeScript loader hangs on those.
  post<P extends RouteWith<'post'>>(route: P, params: Params, body?: Body<P, 'post'>): Response {
    return this.send('post', route, params, body);
  }

  put<P extends RouteWith<'put'>>(
    route: P,
    params: Params,
    body?: Body<P, 'put'>,
    query?: string,
  ): Response {
    return this.send('put', route, params, body, query);
  }

  patch<P extends RouteWith<'patch'>>(route: P, params: Params, body?: Body<P, 'patch'>): Response {
    return this.send('patch', route, params, body);
  }

  delete<P extends RouteWith<'delete'>>(route: P, params: Params): Response {
    return this.send('delete', route, params);
  }
}

// An anonymous GET of an iCal feed: a calendar app polling with the ETag of its last fetch. The
// path is the subscriptionPath the API issued; the route only names the request.
export function pollFeed(path: string, route: Route): Response {
  const url = `${config.baseUrl}${path}`;
  const response = http.get(url, {
    headers: conditional(url),
    tags: { name: route, kind: 'ical' },
  });
  remember(url, response);
  expectOk(response, route);
  return response;
}

export function json<T>(response: Response): T {
  return response.json() as unknown as T;
}

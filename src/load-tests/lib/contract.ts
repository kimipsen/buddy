// The API contract, taken from the frontend's generated OpenAPI types (task docs:openapi), so a
// renamed route or a reshaped request body fails `task test:load:typecheck` instead of failing a
// load run (openapi-client-contract.md). Type-only: k6 strips the import, the file is never loaded.
import type { paths } from '../../frontend/buddy/src/app/core/api/buddy-api.ts';

export type Method = 'get' | 'post' | 'put' | 'patch' | 'delete';

export type Route = keyof paths;

// The routes that answer a given method.
export type RouteWith<M extends Method> = {
  [P in Route]: [NonNullable<paths[P][M]>] extends [never] ? never : P;
}[Route];

type JsonBody<Op> = Op extends { requestBody?: { content: { 'application/json': infer B } } }
  ? B
  : undefined;

export type Body<P extends Route, M extends Method> = JsonBody<NonNullable<paths[P][M]>>;

export type Params = Record<string, string>;

// '/calendars/{calendarId}/occurrences' + { calendarId } + 'from=…' -> the request path. The route
// template doubles as the request's name tag, so metrics group by route, never by id.
export function pathOf(route: Route, params: Params = {}, query = ''): string {
  const path = route.replace(/\{(\w+)\}/g, (_, key: string) => {
    if (!(key in params)) {
      throw new Error(`Missing path parameter '${key}' for ${route}`);
    }
    return encodeURIComponent(params[key]);
  });

  return query ? `${path}?${query}` : path;
}

import { Route } from '@angular/router';
import { describe, expect, it } from 'vitest';

import { SCREENSHOT_PAGES, UNCAPTURED_ROUTES } from '../../screenshots/pages';
import { routes } from './app.routes';

// Every page in the app must appear in the documentation screenshots (docs/screenshots, generated
// by `task docs:screenshots`). Adding a route without adding it to screenshots/pages.ts -- or, when
// it really can't be captured, to UNCAPTURED_ROUTES with a reason -- fails here.

function join(parent: string, path: string | undefined): string {
  return [parent, path].filter(Boolean).join('/').replace(/\/+/g, '/');
}

// Full route patterns of every page: lazy children are loaded, the root redirect ('' with no
// component) and the '**' fallback aren't pages.
async function collectPagePaths(config: Route[], parent = ''): Promise<string[]> {
  const paths: string[] = [];

  for (const route of config) {
    if (route.path === '**' || route.redirectTo !== undefined) {
      continue;
    }

    const full = join(parent, route.path);
    const children =
      route.children ?? ((await route.loadChildren?.()) as Route[] | undefined) ?? [];

    if ((route.component ?? route.loadComponent) && !route.children?.length) {
      paths.push(`/${full}`);
    }

    paths.push(...(await collectPagePaths(children, full)));
  }

  return paths;
}

describe('documentation screenshot coverage', () => {
  it('has a screenshot (or a documented exemption) for every page route', async () => {
    const covered = new Set([
      ...SCREENSHOT_PAGES.map((page) => page.route),
      ...Object.keys(UNCAPTURED_ROUTES),
    ]);

    const missing = (await collectPagePaths(routes)).filter((path) => !covered.has(path));

    expect(
      missing,
      'Add these routes to screenshots/pages.ts and run `task docs:screenshots`',
    ).toEqual([]);
  });

  it('only lists routes that exist', async () => {
    const pages = new Set(await collectPagePaths(routes));
    const listed = [
      ...SCREENSHOT_PAGES.map((page) => page.route),
      ...Object.keys(UNCAPTURED_ROUTES),
    ];

    expect(listed.filter((route) => !pages.has(route))).toEqual([]);
  });

  it('uses each screenshot file name once', () => {
    const names = SCREENSHOT_PAGES.map((page) => page.name);

    expect(names.length).toBe(new Set(names).size);
  });
});

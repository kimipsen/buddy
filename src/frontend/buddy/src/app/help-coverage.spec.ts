import { Route } from '@angular/router';
import { describe, expect, it } from 'vitest';

import { helpTopicKeys } from './core/help/help-topic';
import { HELP_TOPICS } from './core/help/help-topics';
import { en } from './core/i18n/translations/en';
import { GUARDIAN_ROUTES } from './features/guardian/guardian.routes';
import { GuardianShell } from './features/guardian/shell/guardian-shell';

// Every guardian page inside the shell has in-app help (docs/frontend/analysis/in-app-help.md,
// Decision 6), the same way screenshot-coverage.spec.ts keeps the documentation screenshots
// complete. A new page needs a `data.helpTopic` on its route -- or, when help makes no sense for
// it, an entry here with a reason.
const ROUTES_WITHOUT_HELP: Readonly<Record<string, string>> = {
  onboarding: 'The guided setup explains each step itself.',
  help: 'The help page is the help.',
};

const shellPages: Route[] =
  GUARDIAN_ROUTES.find((route) => route.component === GuardianShell)?.children ?? [];

function lookup(key: string): unknown {
  return key
    .split('.')
    .reduce<unknown>(
      (node, part) =>
        node && typeof node === 'object' ? (node as Record<string, unknown>)[part] : undefined,
      en,
    );
}

// Dotted paths of every string under `node`.
function leafKeys(node: unknown, prefix: string): string[] {
  if (typeof node === 'string') {
    return [prefix];
  }

  return Object.entries(node as Record<string, unknown>).flatMap(([key, child]) =>
    leafKeys(child, `${prefix}.${key}`),
  );
}

describe('in-app help coverage', () => {
  it('finds the guardian pages', () => {
    expect(shellPages.length).toBeGreaterThan(10);
  });

  it('has a help topic (or a documented exemption) for every guardian page', () => {
    const missing = shellPages
      .filter((route) => !route.data?.['helpTopic'] && !((route.path ?? '') in ROUTES_WITHOUT_HELP))
      .map((route) => route.path);

    expect(missing, 'Add data: { helpTopic } to these routes in guardian.routes.ts').toEqual([]);
  });

  it('only exempts routes that exist and have no topic', () => {
    for (const path of Object.keys(ROUTES_WITHOUT_HELP)) {
      const route = shellPages.find((candidate) => candidate.path === path);
      expect(route, path).toBeDefined();
      expect(route?.data?.['helpTopic'], path).toBeUndefined();
    }
  });

  it('only names topics that exist', () => {
    const ids = new Set(HELP_TOPICS.map((topic) => topic.id));
    const named = shellPages.flatMap((route) =>
      route.data?.['helpTopic'] ? [route.data['helpTopic'] as string] : [],
    );
    const related = HELP_TOPICS.flatMap((topic) => topic.related ?? []);

    expect([...named, ...related].filter((id) => !ids.has(id))).toEqual([]);
  });

  it('has a topic id and section ids that are unique', () => {
    const ids = HELP_TOPICS.map((topic) => topic.id);
    expect(new Set(ids).size).toBe(ids.length);

    for (const topic of HELP_TOPICS) {
      const sectionIds = topic.sections.map((section) => section.id);
      expect(new Set(sectionIds).size, topic.id).toBe(sectionIds.length);
    }
  });

  it('has English text for every key the registry implies (parity covers Danish)', () => {
    const missing = HELP_TOPICS.flatMap(helpTopicKeys).filter(
      (key) => typeof lookup(key) !== 'string',
    );

    expect(missing).toEqual([]);
  });

  it('has no help text the registry leaves out', () => {
    const implied = new Set(HELP_TOPICS.flatMap(helpTopicKeys));
    const orphans = leafKeys(en.help.topics, 'help.topics').filter((key) => !implied.has(key));

    expect(orphans).toEqual([]);
  });
});

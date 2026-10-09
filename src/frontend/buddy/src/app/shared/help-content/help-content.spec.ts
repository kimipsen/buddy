import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it } from 'vitest';

import { HelpTopic } from '../../core/help/help-topic';
import { HelpContent } from './help-content';

// A topic with no translations, so the raw keys render and the spec checks structure and order
// rather than the help text itself (help-coverage.spec.ts checks that every real key resolves).
const topic: HelpTopic = {
  id: 'demo',
  sections: [{ id: 'first' }, { id: 'second', steps: 2 }],
};

async function setup(value: HelpTopic, headingLevel?: number) {
  await TestBed.configureTestingModule({
    imports: [HelpContent],
    providers: [provideRouter([])],
  }).compileComponents();

  const fixture = TestBed.createComponent(HelpContent);
  fixture.componentRef.setInput('topic', value);
  if (headingLevel !== undefined) {
    fixture.componentRef.setInput('headingLevel', headingLevel);
  }
  fixture.detectChanges();

  return fixture.nativeElement as HTMLElement;
}

describe('HelpContent', () => {
  it('renders each section heading and body in registry order', async () => {
    const compiled = await setup(topic);

    const headings = Array.from(compiled.querySelectorAll('[role="heading"]')).map((heading) =>
      heading.textContent?.trim(),
    );
    expect(headings).toEqual([
      'help.topics.demo.sections.first.title',
      'help.topics.demo.sections.second.title',
    ]);
    expect(compiled.textContent).toContain('help.topics.demo.sections.first.body');
    expect(compiled.textContent).toContain('help.topics.demo.sections.second.body');
  });

  it('renders steps as an ordered list only for sections that have them', async () => {
    const compiled = await setup(topic);

    const lists = compiled.querySelectorAll('ol');
    expect(lists).toHaveLength(1);
    expect(Array.from(lists[0].querySelectorAll('li')).map((li) => li.textContent?.trim())).toEqual(
      ['help.topics.demo.sections.second.steps.s1', 'help.topics.demo.sections.second.steps.s2'],
    );
  });

  it('uses heading level 3 by default and the given level otherwise', async () => {
    expect((await setup(topic)).querySelector('[role="heading"]')?.getAttribute('aria-level')).toBe(
      '3',
    );

    TestBed.resetTestingModule();
    expect(
      (await setup(topic, 4)).querySelector('[role="heading"]')?.getAttribute('aria-level'),
    ).toBe('4');
  });

  it('links to the topic page when the topic has one', async () => {
    const compiled = await setup({ ...topic, link: '/guardian/onboarding' });

    const link = compiled.querySelector<HTMLAnchorElement>('a');
    expect(link?.getAttribute('href')).toBe('/guardian/onboarding');
    expect(link?.textContent?.trim()).toBe('help.topics.demo.link');
  });

  it('renders no link for a topic without one', async () => {
    const compiled = await setup(topic);

    expect(compiled.querySelector('a')).toBeNull();
  });
});

import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { HELP_TOPICS } from '../../../core/help/help-topics';
import { TranslationService } from '../../../core/i18n/translation.service';
import { RuntimeConfigService } from '../../../core/runtime-config.service';
import { GuardianHelp } from './help-page';
import { disableFeatures } from '../../../../testing/features-fixture';

@Component({ template: '' })
class Blank {}

// jsdom has no layout, so scrollIntoView doesn't exist; record calls on the prototype instead.
const scrolled: string[] = [];

beforeEach(() => {
  scrolled.length = 0;
  Element.prototype.scrollIntoView = vi.fn(function (this: Element) {
    scrolled.push(this.id);
  });
});

afterEach(() => {
  delete (Element.prototype as Partial<Element>).scrollIntoView;
});

async function open(url: string) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([
        { path: '', component: Blank },
        { path: 'help', component: GuardianHelp },
      ]),
      {
        provide: RuntimeConfigService,
        useValue: { repositoryUrl: 'https://example.test/buddy' } as Partial<RuntimeConfigService>,
      },
    ],
  });

  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(url);
  // Let afterNextRender run.
  harness.detectChanges();
  await new Promise((resolve) => setTimeout(resolve, 0));

  return harness.routeNativeElement as HTMLElement;
}

function title(id: string): string {
  return TestBed.inject(TranslationService).translate(`help.topics.${id}.title`);
}

describe('GuardianHelp', () => {
  it('lists every topic in the table of contents and in full, in registry order', async () => {
    const page = await open('/help');

    const contents = Array.from(page.querySelectorAll('nav button')).map((button) =>
      button.textContent?.trim(),
    );
    const headings = Array.from(page.querySelectorAll('article h3')).map((heading) =>
      heading.textContent?.trim(),
    );
    const expected = HELP_TOPICS.map((topic) => title(topic.id));

    expect(contents).toEqual(expected);
    expect(headings).toEqual(expected);
    expect(page.querySelector('nav')?.getAttribute('aria-label')).toBe('Help topics');
    expect(page.querySelectorAll('app-help-content')).toHaveLength(HELP_TOPICS.length);
  });

  it('scrolls to and focuses the topic named in ?topic=', async () => {
    const page = await open('/help?topic=calendar');

    expect(scrolled).toEqual(['help-topic-calendar']);
    expect(document.activeElement).toBe(page.querySelector('#help-topic-calendar'));
  });

  it('ignores an unknown ?topic=', async () => {
    await open('/help?topic=nope');

    expect(scrolled).toEqual([]);
  });

  it('does not scroll without ?topic=', async () => {
    await open('/help');

    expect(scrolled).toEqual([]);
  });

  it('scrolls to a topic picked from the table of contents', async () => {
    const page = await open('/help');

    const entry = Array.from(page.querySelectorAll<HTMLButtonElement>('nav button')).find(
      (button) => button.textContent?.trim() === title('medicine'),
    );
    entry?.click();

    expect(scrolled).toEqual(['help-topic-medicine']);
  });

  it('points to the family operator and the project repository', async () => {
    const page = await open('/help');

    expect(page.textContent).toContain('Ask whoever runs Buddy for your family');
    const link = page.querySelector<HTMLAnchorElement>('a[href="https://example.test/buddy"]');
    expect(link?.getAttribute('target')).toBe('_blank');
    expect(link?.getAttribute('rel')).toBe('noopener noreferrer');
  });

  it('leaves out the topics of features that are turned off', async () => {
    disableFeatures('medicines', 'printing');
    const page = await open('/help');

    const contents = Array.from(page.querySelectorAll('nav button')).map((button) =>
      button.textContent?.trim(),
    );
    expect(contents).toHaveLength(HELP_TOPICS.length - 2);
    expect(contents).not.toContain(title('medicine'));
    expect(contents).not.toContain(title('print'));
    expect(contents).toContain(title('calendar'));
  });
});

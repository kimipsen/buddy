import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import { HelpTopic } from '../../../../core/help/help-topic';
import { HelpPanel } from './help-panel';

// Raw keys render for the made-up topic; the panel's own strings are real and asserted in English.
const topic: HelpTopic = {
  id: 'demo',
  sections: [{ id: 'first' }],
  related: ['other', 'third'],
};

async function setup(value: HelpTopic = topic) {
  await TestBed.configureTestingModule({
    imports: [HelpPanel],
    providers: [provideRouter([])],
  }).compileComponents();

  const fixture = TestBed.createComponent(HelpPanel);
  fixture.componentRef.setInput('topic', value);
  fixture.componentRef.setInput('panelId', 'page-help');
  const closed = vi.fn();
  fixture.componentInstance.closed.subscribe(closed);
  fixture.detectChanges();

  return { compiled: fixture.nativeElement as HTMLElement, closed };
}

function links(compiled: HTMLElement) {
  return Array.from(compiled.querySelectorAll<HTMLAnchorElement>('a')).map((a) => ({
    text: a.textContent?.trim(),
    href: a.getAttribute('href'),
  }));
}

describe('HelpPanel', () => {
  it('is a region labelled by its title, with the topic content inside', async () => {
    const { compiled } = await setup();

    const region = compiled.querySelector('section#page-help');
    expect(region?.getAttribute('aria-labelledby')).toBe('page-help-title');
    expect(compiled.querySelector('h2#page-help-title')?.textContent?.trim()).toBe(
      'Help: help.topics.demo.title',
    );
    expect(compiled.querySelector('app-help-content')?.textContent).toContain(
      'help.topics.demo.sections.first.body',
    );
  });

  it('links each related topic and all topics to the help page, keyed by topic', async () => {
    const { compiled } = await setup();

    expect(compiled.textContent).toContain('See also:');
    expect(links(compiled)).toEqual([
      { text: 'help.topics.other.title', href: '/guardian/help?topic=other' },
      { text: 'help.topics.third.title', href: '/guardian/help?topic=third' },
      { text: 'All help topics', href: '/guardian/help?topic=demo' },
    ]);
  });

  it('leaves out the related list for a topic without related topics', async () => {
    const { compiled } = await setup({ id: 'demo', sections: [{ id: 'first' }] });

    expect(compiled.textContent).not.toContain('See also:');
    expect(links(compiled)).toEqual([
      { text: 'All help topics', href: '/guardian/help?topic=demo' },
    ]);
  });

  it('emits closed from the close button', async () => {
    const { compiled, closed } = await setup();

    const button = Array.from(compiled.querySelectorAll('button')).find(
      (candidate) => candidate.textContent?.trim() === 'Close help',
    );
    button?.click();

    expect(closed).toHaveBeenCalledTimes(1);
  });
});

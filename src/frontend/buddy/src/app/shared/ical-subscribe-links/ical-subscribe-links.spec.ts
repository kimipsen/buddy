import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';

import { IcalSubscribeLinks } from './ical-subscribe-links';

describe('IcalSubscribeLinks', () => {
  async function setup(url: string) {
    await TestBed.configureTestingModule({ imports: [IcalSubscribeLinks] }).compileComponents();

    const fixture = TestBed.createComponent(IcalSubscribeLinks);
    fixture.componentRef.setInput('url', url);
    fixture.detectChanges();
    const links = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('a'));
    return { compiled: fixture.nativeElement as HTMLElement, links };
  }

  it('links to the feed over webcal: so the calendar app opens its subscribe dialog', async () => {
    const { links } = await setup('https://api.buddy.test/calendars/c-1/ical/secret');

    expect(links[0].textContent?.trim()).toBe('Open in calendar app');
    expect(links[0].getAttribute('href')).toBe('webcal://api.buddy.test/calendars/c-1/ical/secret');
  });

  it('rewrites a plain http feed URL to webcal: as well', async () => {
    const { links } = await setup('http://localhost:5193/mealplans/p-1/ical/secret');

    expect(links[0].getAttribute('href')).toBe('webcal://localhost:5193/mealplans/p-1/ical/secret');
  });

  it('links to Google Calendar with the encoded webcal: URL as cid, in a new tab', async () => {
    const { links } = await setup('https://api.buddy.test/calendars/c-1/ical/secret');

    expect(links[1].textContent?.trim()).toBe('Add to Google Calendar');
    expect(links[1].getAttribute('href')).toBe(
      'https://calendar.google.com/calendar/r?cid=webcal%3A%2F%2Fapi.buddy.test%2Fcalendars%2Fc-1%2Fical%2Fsecret',
    );
    expect(links[1].getAttribute('target')).toBe('_blank');
    expect(links[1].getAttribute('rel')).toBe('noopener noreferrer');
  });

  it('tells the guardian to subscribe rather than import', async () => {
    const { compiled } = await setup('https://api.buddy.test/calendars/c-1/ical/secret');

    expect(compiled.textContent).toContain(
      'Add the link as a subscription, not as an imported file -- an imported file never updates.',
    );
  });
});

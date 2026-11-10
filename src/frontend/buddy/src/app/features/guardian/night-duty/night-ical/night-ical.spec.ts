import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import {
  IssuedNightIcalToken,
  NightDutyService,
  NightIcalTokenSummary,
} from '../../../../core/night-duty.service';
import { NightIcal } from './night-ical';

describe('NightIcal', () => {
  const feedPath = '/night-duty/feeds/guardian-1/ical/plaintext-secret';

  function token(overrides: Partial<NightIcalTokenSummary> = {}): NightIcalTokenSummary {
    return { tokenId: 'token-1', issuedAt: '2026-10-01T00:00:00Z', ...overrides };
  }

  function issuedToken(): IssuedNightIcalToken {
    return {
      tokenId: 'token-new',
      token: 'plaintext-secret',
      subscriptionPath: feedPath,
      issuedAt: '2026-11-10T00:00:00Z',
    };
  }

  async function setup(nightDuty: Partial<NightDutyService> = {}) {
    const stub: Partial<NightDutyService> = {
      listIcalTokens: vi.fn(async () => []),
      createIcalToken: vi.fn(async () => issuedToken()),
      revokeIcalToken: vi.fn(async () => undefined),
      icalFeedUrl: vi.fn((path: string) => `https://api.buddy.test${path}`),
      ...nightDuty,
    };

    await TestBed.configureTestingModule({
      imports: [NightIcal],
      providers: [{ provide: NightDutyService, useValue: stub }],
    }).compileComponents();

    const fixture = TestBed.createComponent(NightIcal);
    fixture.componentRef.setInput('childId', 'child-1');
    fixture.componentRef.setInput('childName', 'Emil');

    return { fixture, nightDuty: stub };
  }

  // A mutation that reloads the token list (create, revoke) only starts the reload on the next
  // change detection, so flush a second macrotask round to let the reloaded list render.
  async function settle(fixture: { detectChanges: () => void }) {
    for (let round = 0; round < 2; round++) {
      fixture.detectChanges();
      await new Promise((resolve) => setTimeout(resolve, 0));
    }
    fixture.detectChanges();
  }

  function findButtonByText(compiled: HTMLElement, text: string): HTMLButtonElement | undefined {
    return Array.from(compiled.querySelectorAll('button')).find(
      (button) => button.textContent?.trim() === text,
    );
  }

  it('explains the feed for the child and shows the empty state when there are no links', async () => {
    const { fixture, nightDuty } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(nightDuty.listIcalTokens).toHaveBeenCalledWith('child-1');
    expect(compiled.textContent).toContain("Emil's nights in my calendar");
    expect(compiled.textContent).toContain("puts Emil's night duty into your own calendar app");
    expect(compiled.textContent).toContain('Each planned night is an all-day event');
    expect(compiled.textContent).toContain('One link per child.');
    expect(compiled.textContent).toContain('No subscription links yet.');
  });

  it('shows a loading message until the links arrive', async () => {
    const { fixture } = await setup({ listIcalTokens: vi.fn(() => new Promise<never>(() => {})) });
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      'Loading subscription links…',
    );
  });

  it('shows an error when loading the links fails', async () => {
    const { fixture } = await setup({
      listIcalTokens: vi.fn(async () => Promise.reject(new Error('boom'))),
    });
    await settle(fixture);

    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      'Unable to load subscription links.',
    );
  });

  it('lists existing links with their issue date and a revoke button each', async () => {
    const tokens = [
      token({ tokenId: 'token-a', issuedAt: '2026-10-12T12:00:00Z' }),
      token({ tokenId: 'token-b' }),
    ];
    const { fixture } = await setup({ listIcalTokens: vi.fn(async () => tokens) });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Issued Oct 12, 2026');
    expect(
      Array.from(compiled.querySelectorAll('button')).filter(
        (button) => button.textContent?.trim() === 'Revoke',
      ),
    ).toHaveLength(2);
  });

  it('creates a link for the child and shows the plaintext feed URL with subscribe links', async () => {
    const { fixture, nightDuty } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Create subscription link')!.click();
    fixture.detectChanges();

    expect(findButtonByText(compiled, 'Creating…')!.disabled).toBe(true);

    await settle(fixture);

    expect(nightDuty.createIcalToken).toHaveBeenCalledWith('child-1');
    expect(nightDuty.icalFeedUrl).toHaveBeenCalledWith(feedPath);
    expect(compiled.textContent).toContain(`https://api.buddy.test${feedPath}`);
    expect(compiled.textContent).toContain('it will not be shown again');
    expect(compiled.querySelector('app-ical-subscribe-links a')!.getAttribute('href')).toBe(
      `webcal://api.buddy.test${feedPath}`,
    );
    expect(nightDuty.listIcalTokens).toHaveBeenCalledTimes(2);
  });

  it('hides the new URL and loads the other child’s links when another child is picked', async () => {
    const { fixture, nightDuty } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Create subscription link')!.click();
    await settle(fixture);
    expect(compiled.textContent).toContain('it will not be shown again');

    fixture.componentRef.setInput('childId', 'child-2');
    fixture.componentRef.setInput('childName', 'Ida');
    await settle(fixture);

    expect(nightDuty.listIcalTokens).toHaveBeenLastCalledWith('child-2');
    expect(compiled.textContent).toContain("Ida's nights in my calendar");
    expect(compiled.textContent).not.toContain('it will not be shown again');
  });

  it('shows an error when creating a link fails', async () => {
    const { fixture } = await setup({
      createIcalToken: vi.fn(async () => Promise.reject(new Error('boom'))),
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Create subscription link')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to create a subscription link.');
    expect(compiled.textContent).not.toContain('it will not be shown again');
  });

  it('copies the newly issued feed URL to the clipboard and shows a confirmation', async () => {
    const writeText = vi.fn(async () => undefined);
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });

    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Create subscription link')!.click();
    await settle(fixture);
    findButtonByText(compiled, 'Copy link')!.click();
    await settle(fixture);

    expect(writeText).toHaveBeenCalledWith(`https://api.buddy.test${feedPath}`);
    expect(findButtonByText(compiled, 'Copied!')).toBeTruthy();
  });

  it('keeps the copy button unconfirmed when the clipboard refuses', async () => {
    const writeText = vi.fn(async () => Promise.reject(new Error('denied')));
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });

    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Create subscription link')!.click();
    await settle(fixture);
    findButtonByText(compiled, 'Copy link')!.click();
    await settle(fixture);

    expect(findButtonByText(compiled, 'Copy link')).toBeTruthy();
    expect(findButtonByText(compiled, 'Copied!')).toBeUndefined();
  });

  it('revokes a link for the child and reloads the list', async () => {
    let loadCount = 0;
    const listIcalTokens = vi.fn(async () =>
      loadCount++ === 0 ? [token({ tokenId: 'token-a' })] : [],
    );
    const { fixture, nightDuty } = await setup({ listIcalTokens });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Revoke')!.click();
    await settle(fixture);

    expect(nightDuty.revokeIcalToken).toHaveBeenCalledWith('child-1', 'token-a');
    expect(listIcalTokens).toHaveBeenCalledTimes(2);
    expect(compiled.textContent).toContain('No subscription links yet.');
  });

  it('drops a create error when another child is picked', async () => {
    const { fixture } = await setup({
      createIcalToken: vi.fn(async () => Promise.reject(new Error('boom'))),
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Create subscription link')!.click();
    await settle(fixture);
    expect(compiled.textContent).toContain('Unable to create a subscription link.');

    fixture.componentRef.setInput('childId', 'child-2');
    await settle(fixture);

    expect(compiled.textContent).not.toContain('Unable to create a subscription link.');
  });

  it('shows an error when revoking a link fails', async () => {
    const { fixture } = await setup({
      listIcalTokens: vi.fn(async () => [token({ tokenId: 'token-a' })]),
      revokeIcalToken: vi.fn(async () => Promise.reject(new Error('boom'))),
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Revoke')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to revoke that subscription link.');
  });
});

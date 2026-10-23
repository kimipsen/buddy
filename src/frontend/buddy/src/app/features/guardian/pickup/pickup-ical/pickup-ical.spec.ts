import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import {
  IssuedPickupIcalToken,
  PickupIcalTokenSummary,
  PickupsService,
} from '../../../../core/pickups.service';
import { PickupIcal } from './pickup-ical';

describe('PickupIcal', () => {
  function token(overrides: Partial<PickupIcalTokenSummary> = {}): PickupIcalTokenSummary {
    return { tokenId: 'token-1', issuedAt: '2026-10-01T00:00:00Z', ...overrides };
  }

  function issuedToken(overrides: Partial<IssuedPickupIcalToken> = {}): IssuedPickupIcalToken {
    return {
      tokenId: 'token-new',
      token: 'plaintext-secret',
      subscriptionPath: '/pickups/feeds/guardian-1/ical/plaintext-secret',
      ...overrides,
    };
  }

  async function setup(pickups: Partial<PickupsService> = {}) {
    const pickupsStub: Partial<PickupsService> = {
      listIcalTokens: vi.fn(async () => []),
      createIcalToken: vi.fn(async () => issuedToken()),
      revokeIcalToken: vi.fn(async () => undefined),
      icalFeedUrl: vi.fn((path: string) => `https://api.buddy.test${path}`),
      ...pickups,
    };

    await TestBed.configureTestingModule({
      imports: [PickupIcal],
      providers: [{ provide: PickupsService, useValue: pickupsStub }],
    }).compileComponents();

    const fixture = TestBed.createComponent(PickupIcal);

    return { fixture, pickups: pickupsStub };
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

  it('explains what the feed holds and shows the empty state when there are no tokens', async () => {
    const { fixture, pickups } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(pickups.listIcalTokens).toHaveBeenCalledTimes(1);
    expect(compiled.textContent).toContain('My pickups in my calendar');
    expect(compiled.textContent).toContain(
      'Only the slots assigned to you, not those of other guardians, siblings, babysitters or playdates.',
    );
    expect(compiled.textContent).toContain('No subscription links yet.');
  });

  it('shows a loading message until the tokens arrive', async () => {
    const { fixture } = await setup({ listIcalTokens: vi.fn(() => new Promise<never>(() => {})) });
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      'Loading subscription links…',
    );
  });

  it('shows an error when loading tokens fails', async () => {
    const { fixture } = await setup({
      listIcalTokens: vi.fn(async () => Promise.reject(new Error('boom'))),
    });
    await settle(fixture);

    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      'Unable to load subscription links.',
    );
  });

  it('lists existing tokens with their issue date and a revoke button each', async () => {
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

  it('creates a new token and shows the plaintext feed URL with subscribe links', async () => {
    const { fixture, pickups } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Create subscription link')!.click();
    fixture.detectChanges();

    expect(findButtonByText(compiled, 'Creating…')!.disabled).toBe(true);

    await settle(fixture);

    expect(pickups.createIcalToken).toHaveBeenCalledWith();
    expect(pickups.icalFeedUrl).toHaveBeenCalledWith(
      '/pickups/feeds/guardian-1/ical/plaintext-secret',
    );
    expect(compiled.textContent).toContain(
      'https://api.buddy.test/pickups/feeds/guardian-1/ical/plaintext-secret',
    );
    expect(compiled.textContent).toContain('it will not be shown again');
    expect(compiled.querySelector('app-ical-subscribe-links a')!.getAttribute('href')).toBe(
      'webcal://api.buddy.test/pickups/feeds/guardian-1/ical/plaintext-secret',
    );
    expect(pickups.listIcalTokens).toHaveBeenCalledTimes(2);
  });

  it('shows an error when creating a token fails', async () => {
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

    expect(writeText).toHaveBeenCalledWith(
      'https://api.buddy.test/pickups/feeds/guardian-1/ical/plaintext-secret',
    );
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

  it('revokes a token and reloads the token list', async () => {
    let loadCount = 0;
    const listIcalTokens = vi.fn(async () =>
      loadCount++ === 0 ? [token({ tokenId: 'token-a' })] : [],
    );
    const { fixture, pickups } = await setup({ listIcalTokens });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Revoke')!.click();
    await settle(fixture);

    expect(pickups.revokeIcalToken).toHaveBeenCalledWith('token-a');
    expect(listIcalTokens).toHaveBeenCalledTimes(2);
    expect(compiled.textContent).toContain('No subscription links yet.');
  });

  it('shows an error when revoking a token fails', async () => {
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

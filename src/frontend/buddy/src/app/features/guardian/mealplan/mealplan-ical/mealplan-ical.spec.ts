import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { IssuedMealplanIcalToken, MealplanIcalTokenSummary, MealplansService } from '../../../../core/mealplans.service';
import { MealplanIcal } from './mealplan-ical';

describe('MealplanIcal', () => {
  function token(overrides: Partial<MealplanIcalTokenSummary> = {}): MealplanIcalTokenSummary {
    return { tokenId: 'token-1', issuedAt: '2026-08-01T00:00:00Z', ...overrides };
  }

  function issuedToken(overrides: Partial<IssuedMealplanIcalToken> = {}): IssuedMealplanIcalToken {
    return { tokenId: 'token-new', token: 'plaintext-secret', subscriptionPath: '/mealplans/plan-1/ical/token-new', ...overrides };
  }

  async function setup(mealplans: Partial<MealplansService> = {}) {
    const mealplansStub: Partial<MealplansService> = {
      listIcalTokens: vi.fn(async () => []),
      createIcalToken: vi.fn(async () => issuedToken()),
      revokeIcalToken: vi.fn(async () => undefined),
      icalFeedUrl: vi.fn((path: string) => `https://api.buddy.test${path}`),
      ...mealplans
    };

    await TestBed.configureTestingModule({
      imports: [MealplanIcal],
      providers: [{ provide: MealplansService, useValue: mealplansStub }]
    }).compileComponents();

    const fixture = TestBed.createComponent(MealplanIcal);
    fixture.componentRef.setInput('childId', 'child-1');

    return { fixture, mealplans: mealplansStub };
  }

  async function settle(fixture: { detectChanges: () => void }) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();
  }

  function findButtonByText(compiled: HTMLElement, text: string): HTMLButtonElement | undefined {
    return Array.from(compiled.querySelectorAll('button')).find((button) => button.textContent?.trim() === text);
  }

  it('loads tokens for the given child on init', async () => {
    const { fixture, mealplans } = await setup();
    await settle(fixture);

    expect(mealplans.listIcalTokens).toHaveBeenCalledWith('child-1');
  });

  it('shows the empty state when there are no tokens', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('No subscription links yet.');
  });

  it('shows an error when loading tokens fails', async () => {
    const { fixture } = await setup({ listIcalTokens: vi.fn(async () => Promise.reject(new Error('boom'))) });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Unable to load subscription links.');
  });

  it('lists existing tokens with a revoke button each', async () => {
    const tokens = [token({ tokenId: 'token-a' }), token({ tokenId: 'token-b' })];
    const { fixture } = await setup({ listIcalTokens: vi.fn(async () => tokens) });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(Array.from(compiled.querySelectorAll('button')).filter((button) => button.textContent?.trim() === 'Revoke')).toHaveLength(2);
  });

  it('creates a new token and shows the plaintext feed URL with a copy button', async () => {
    const { fixture, mealplans } = await setup({
      createIcalToken: vi.fn(async () => issuedToken({ subscriptionPath: '/mealplans/plan-1/ical/token-new' }))
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Create subscription link')!.click();
    fixture.detectChanges();

    expect(compiled.textContent).toContain('Creating…');
    expect(findButtonByText(compiled, 'Creating…')!.disabled).toBe(true);

    await settle(fixture);

    expect(mealplans.icalFeedUrl).toHaveBeenCalledWith('/mealplans/plan-1/ical/token-new');
    expect(compiled.textContent).toContain('https://api.buddy.test/mealplans/plan-1/ical/token-new');
    expect(findButtonByText(compiled, 'Copy link')).toBeTruthy();
    expect(mealplans.listIcalTokens).toHaveBeenCalledTimes(2);
  });

  it('shows an error when creating a token fails', async () => {
    const { fixture } = await setup({ createIcalToken: vi.fn(async () => Promise.reject(new Error('boom'))) });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Create subscription link')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to create a subscription link.');
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

    expect(writeText).toHaveBeenCalledWith(`https://api.buddy.test${issuedToken().subscriptionPath}`);
    expect(findButtonByText(compiled, 'Copied!')).toBeTruthy();
  });

  it('revokes a token and reloads the token list', async () => {
    let loadCount = 0;
    const tokens = [token({ tokenId: 'token-a' })];
    const listIcalTokens = vi.fn(async () => (loadCount++ === 0 ? tokens : []));
    const { fixture, mealplans } = await setup({ listIcalTokens });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Revoke')!.click();
    await settle(fixture);

    expect(mealplans.revokeIcalToken).toHaveBeenCalledWith('child-1', 'token-a');
    expect(listIcalTokens).toHaveBeenCalledTimes(2);
    expect(compiled.textContent).toContain('No subscription links yet.');
  });

  it('shows an error when revoking a token fails', async () => {
    const tokens = [token({ tokenId: 'token-a' })];
    const { fixture } = await setup({
      listIcalTokens: vi.fn(async () => tokens),
      revokeIcalToken: vi.fn(async () => Promise.reject(new Error('boom')))
    });
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    findButtonByText(compiled, 'Revoke')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to revoke that subscription link.');
  });
});

import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import {
  AiAssistantService,
  AiProviderSettings,
  AiSessionView,
} from '../../../../core/ai-assistant.service';
import { ChildSummary, GuardiansService } from '../../../../core/guardians.service';
import { MealplanAiAssistant } from './ai-assistant';

describe('MealplanAiAssistant', () => {
  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  function child(overrides: Partial<ChildSummary> = {}): ChildSummary {
    return {
      id: 'child-1',
      name: { givenName: 'Alex', familyName: 'Doe' },
      guardianLinkId: 'link-1',
      kind: 'Parent',
      language: 'en',
      timeZoneId: 'UTC',
      ...overrides,
    };
  }

  function providerSettings(overrides: Partial<AiProviderSettings> = {}): AiProviderSettings {
    return {
      providers: [{ provider: 'Anthropic', last4: '1234', addedAt: '2026-08-01T00:00:00Z' }],
      activeProvider: 'Anthropic',
      dataSharingAcknowledgedAt: '2026-08-01T00:00:00Z',
      ...overrides,
    };
  }

  function session(overrides: Partial<AiSessionView> = {}): AiSessionView {
    return {
      id: 'session-1',
      from: '2026-08-01',
      to: '2026-08-03',
      requestedSlots: ['Dinner'],
      status: 'Drafting',
      transcript: [],
      draft: [],
      ratedOnly: false,
      servedWithin: 'Any',
      ...overrides,
    };
  }

  function notFound(): HttpErrorResponse {
    return new HttpErrorResponse({ status: 404, statusText: 'Not Found' });
  }

  function noMatchingMeals(): HttpErrorResponse {
    return new HttpErrorResponse({
      status: 400,
      error: { code: 'validation_error', details: { ServedWithin: ['No meals match.'] } },
    });
  }

  interface Stubs {
    guardians?: Partial<GuardiansService>;
    aiAssistant?: Partial<AiAssistantService>;
  }

  async function setup(stubs: Stubs = {}) {
    const guardiansStub: Partial<GuardiansService> = {
      listMyChildren: vi.fn(async () => [child()]),
      ...stubs.guardians,
    };
    const aiAssistantStub: Partial<AiAssistantService> = {
      listProviders: vi.fn(async () => providerSettings()),
      getCurrentSession: vi.fn(async () => Promise.reject(notFound())),
      startSession: vi.fn(async () => session()),
      sendMessage: vi.fn(async () => session()),
      applyDraft: vi.fn(async () => session({ status: 'Applied' })),
      discardSession: vi.fn(async () => session({ status: 'Discarded' })),
      ...stubs.aiAssistant,
    };

    await TestBed.configureTestingModule({
      imports: [MealplanAiAssistant],
      providers: [
        provideRouter([]),
        { provide: GuardiansService, useValue: guardiansStub },
        { provide: AiAssistantService, useValue: aiAssistantStub },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(MealplanAiAssistant);

    return { fixture, guardians: guardiansStub, aiAssistant: aiAssistantStub };
  }

  async function settle(fixture: {
    detectChanges: () => void;
    whenStable: () => Promise<boolean>;
  }) {
    fixture.detectChanges();

    for (let i = 0; i < 10; i++) {
      await fixture.whenStable();
      fixture.detectChanges();
    }
  }

  function findButtonByText(compiled: HTMLElement, text: string): HTMLButtonElement | undefined {
    return Array.from(compiled.querySelectorAll('button')).find(
      (button) => button.textContent?.trim() === text,
    );
  }

  it('shows a hint when the guardian has no linked children', async () => {
    const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => []) } });
    await settle(fixture);

    expect(fixture.nativeElement.textContent).toContain(
      'Link a child from Settings before using the AI assistant.',
    );
  });

  it('prompts to configure a provider before showing the start form', async () => {
    const { fixture } = await setup({
      aiAssistant: { listProviders: vi.fn(async () => providerSettings({ activeProvider: null })) },
    });
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    expect(compiled.textContent).toContain(
      'Add an AI provider API key in Settings before starting a session.',
    );
    expect(compiled.querySelector('form')).toBeNull();
  });

  it('shows what is shared, instead of the start form, until data sharing is acknowledged', async () => {
    const acknowledgeDataSharing = vi.fn(async () =>
      providerSettings({ dataSharingAcknowledgedAt: '2026-08-02T00:00:00Z' }),
    );
    const { fixture } = await setup({
      aiAssistant: {
        listProviders: vi.fn(async () => providerSettings({ dataSharingAcknowledgedAt: null })),
        acknowledgeDataSharing,
      },
    });
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    expect(compiled.textContent).toContain('What the assistant shares');
    expect(compiled.querySelector('form')).toBeNull();

    findButtonByText(compiled, 'I understand, continue')!.click();
    await settle(fixture);

    expect(acknowledgeDataSharing).toHaveBeenCalledWith('child-1');
    expect(compiled.textContent).not.toContain('What the assistant shares');
    expect(compiled.textContent).toContain('Start a new session');
  });

  it('shows the start form with a default slot selected when there is no current session', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    expect(compiled.textContent).toContain('Start a new session');
    expect(findButtonByText(compiled, 'Dinner')).toBeTruthy();
  });

  it('restores an in-progress session on load', async () => {
    const { fixture } = await setup({
      aiAssistant: { getCurrentSession: vi.fn(async () => session()) },
    });
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    expect(compiled.textContent).toContain('Drafting');
    expect(compiled.querySelector('input[name="aiMessage"]')).toBeTruthy();
  });

  it('starts a session and shows the chat once at least one slot is selected', async () => {
    const { fixture, aiAssistant } = await setup();
    await settle(fixture);

    findButtonByText(fixture.nativeElement, 'Start planning')!.click();
    await settle(fixture);

    expect(aiAssistant.startSession).toHaveBeenCalledWith(
      'child-1',
      expect.objectContaining({ slots: ['Dinner'], mustIncludeMealIds: [], notes: '' }),
    );
    expect(fixture.nativeElement.textContent).toContain('Drafting');
  });

  it('moves the last day along with the first day, keeping the period length', async () => {
    const { fixture, aiAssistant } = await setup();
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    const from = compiled.querySelector<HTMLInputElement>('#aiFrom')!;
    const to = compiled.querySelector<HTMLInputElement>('#aiTo')!;
    from.value = '2026-08-01';
    from.dispatchEvent(new Event('input'));
    to.value = '2026-08-03';
    to.dispatchEvent(new Event('input'));
    await settle(fixture);
    from.value = '2026-08-10';
    from.dispatchEvent(new Event('input'));
    await settle(fixture);

    expect(to.value).toBe('2026-08-12');
    findButtonByText(compiled, 'Start planning')!.click();
    await settle(fixture);
    expect(aiAssistant.startSession).toHaveBeenCalledWith(
      'child-1',
      expect.objectContaining({ from: '2026-08-10', to: '2026-08-12' }),
    );
  });

  it('sends the chosen meal filter and remembers it on this device', async () => {
    const { fixture, aiAssistant } = await setup();
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    compiled.querySelector<HTMLButtonElement>('button[role="switch"]')!.click();
    findButtonByText(compiled, '60 days')!.click();
    await settle(fixture);
    findButtonByText(compiled, 'Start planning')!.click();
    await settle(fixture);

    expect(aiAssistant.startSession).toHaveBeenCalledWith(
      'child-1',
      expect.objectContaining({ ratedOnly: true, servedWithin: 'Last60Days' }),
    );
    expect(JSON.parse(localStorage.getItem('buddy_ai_meal_filter')!)).toEqual({
      ratedOnly: true,
      servedWithin: 'Last60Days',
    });
  });

  it('starts unfiltered by default', async () => {
    const { fixture, aiAssistant } = await setup();
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    expect(compiled.querySelector('button[role="switch"]')!.getAttribute('aria-checked')).toBe(
      'false',
    );
    expect(findButtonByText(compiled, 'Any time')!.getAttribute('aria-checked')).toBe('true');

    findButtonByText(compiled, 'Start planning')!.click();
    await settle(fixture);

    expect(aiAssistant.startSession).toHaveBeenCalledWith(
      'child-1',
      expect.objectContaining({ ratedOnly: false, servedWithin: 'Any' }),
    );
  });

  it('pre-fills the filter last used on this device', async () => {
    localStorage.setItem(
      'buddy_ai_meal_filter',
      JSON.stringify({ ratedOnly: true, servedWithin: 'Last30Days' }),
    );
    const { fixture } = await setup();
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    expect(compiled.querySelector('button[role="switch"]')!.getAttribute('aria-checked')).toBe(
      'true',
    );
    expect(findButtonByText(compiled, '30 days')!.getAttribute('aria-checked')).toBe('true');
  });

  it('does not remember a filter whose session failed to start', async () => {
    const { fixture } = await setup({
      aiAssistant: {
        startSession: vi.fn(async () =>
          Promise.reject(new HttpErrorResponse({ status: 500, statusText: 'Server Error' })),
        ),
      },
    });
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    findButtonByText(compiled, '90 days')!.click();
    await settle(fixture);
    findButtonByText(compiled, 'Start planning')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain(
      'Unable to start a session. Check that a provider is configured.',
    );
    expect(localStorage.getItem('buddy_ai_meal_filter')).toBeNull();
  });

  it('explains when the filter matches no meals', async () => {
    const { fixture } = await setup({
      aiAssistant: {
        startSession: vi.fn(async () => Promise.reject(noMatchingMeals())),
      },
    });
    await settle(fixture);

    findButtonByText(fixture.nativeElement, 'Start planning')!.click();
    await settle(fixture);

    expect(fixture.nativeElement.textContent).toContain(
      'No meals match this filter. Choose a longer period or include unrated meals.',
    );
  });

  it.each([
    [true, 'Last60Days', 'Using rated meals served in the 60 days before 2026-08-01.'],
    [false, 'Last30Days', 'Using meals served in the 30 days before 2026-08-01.'],
    [true, 'Any', 'Using only meals the children have rated.'],
  ] as const)(
    'summarises the session filter (ratedOnly=%s, servedWithin=%s)',
    async (ratedOnly, servedWithin, expected) => {
      const { fixture } = await setup({
        aiAssistant: {
          getCurrentSession: vi.fn(async () => session({ ratedOnly, servedWithin })),
        },
      });
      await settle(fixture);

      expect(fixture.nativeElement.textContent).toContain(expected);
    },
  );

  it('shows no filter summary for an unfiltered session', async () => {
    const { fixture } = await setup({
      aiAssistant: { getCurrentSession: vi.fn(async () => session()) },
    });
    await settle(fixture);

    expect(fixture.nativeElement.textContent).not.toContain('Using ');
  });

  it('explains when the session filter no longer matches any meal', async () => {
    const { fixture } = await setup({
      aiAssistant: {
        getCurrentSession: vi.fn(async () => session({ servedWithin: 'Last30Days' })),
        sendMessage: vi.fn(async () => Promise.reject(noMatchingMeals())),
      },
    });
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    const input = compiled.querySelector<HTMLInputElement>('input[name="aiMessage"]')!;
    input.value = 'Plan dinners.';
    input.dispatchEvent(new Event('input'));
    await settle(fixture);
    findButtonByText(compiled, 'Send')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain(
      "No meals match this session's filter any more. Start a new session.",
    );
  });

  it('does not start a session when no slot is selected', async () => {
    const { fixture, aiAssistant } = await setup();
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    findButtonByText(compiled, 'Dinner')!.click(); // deselect the default slot
    await settle(fixture);

    expect(findButtonByText(compiled, 'Start planning')!.disabled).toBe(true);
    expect(aiAssistant.startSession).not.toHaveBeenCalled();
  });

  it('sends a message and appends the reply to the transcript', async () => {
    const { fixture, aiAssistant } = await setup({
      aiAssistant: {
        getCurrentSession: vi.fn(async () => session()),
        sendMessage: vi.fn(async () =>
          session({
            transcript: [
              { role: 'User', text: 'Plan five dinners.', occurredAt: '2026-08-01T00:00:00Z' },
            ],
          }),
        ),
      },
    });
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    const input = compiled.querySelector<HTMLInputElement>('input[name="aiMessage"]')!;
    input.value = 'Plan five dinners.';
    input.dispatchEvent(new Event('input'));
    await settle(fixture);

    findButtonByText(compiled, 'Send')!.click();
    await settle(fixture);

    expect(aiAssistant.sendMessage).toHaveBeenCalledWith('child-1', 'Plan five dinners.');
    expect(compiled.textContent).toContain('Plan five dinners.');
  });

  it('applies the draft and shows the applied outcome with a way to start over', async () => {
    const { fixture, aiAssistant } = await setup({
      aiAssistant: {
        getCurrentSession: vi.fn(async () =>
          session({
            draft: [{ date: '2026-08-01', slot: 'Dinner', mealId: 'meal-1', mealName: 'Tacos' }],
          }),
        ),
      },
    });
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    expect(compiled.textContent).toContain('Tacos');

    findButtonByText(compiled, 'Apply to my meal plan')!.click();
    await settle(fixture);

    expect(aiAssistant.applyDraft).toHaveBeenCalledWith('child-1');
    expect(compiled.textContent).toContain('Applied to your meal plan');

    findButtonByText(compiled, 'Start a new session')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Start a new session');
    expect(compiled.querySelector('form')).toBeTruthy();
  });

  it('discards the session after confirming', async () => {
    const { fixture, aiAssistant } = await setup({
      aiAssistant: { getCurrentSession: vi.fn(async () => session()) },
    });
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    findButtonByText(compiled, 'Discard')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Discard this session? This cannot be undone.');

    findButtonByText(compiled, 'Confirm')!.click();
    await settle(fixture);

    expect(aiAssistant.discardSession).toHaveBeenCalledWith('child-1');
    expect(compiled.textContent).toContain('Discarded');
  });
});

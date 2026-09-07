import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';

import { AiAssistantService, AiProviderSettings, AiSessionView } from '../../../../core/ai-assistant.service';
import { ChildSummary, GuardiansService } from '../../../../core/guardians.service';
import { MealplanAiAssistant } from './ai-assistant';

describe('MealplanAiAssistant', () => {
  function child(overrides: Partial<ChildSummary> = {}): ChildSummary {
    return { id: 'child-1', name: { givenName: 'Alex', familyName: 'Doe' }, guardianLinkId: 'link-1', kind: 0, language: 'en', timeZoneId: 'UTC', ...overrides };
  }

  function providerSettings(overrides: Partial<AiProviderSettings> = {}): AiProviderSettings {
    return { providers: [{ provider: 0, last4: '1234', addedAt: '2026-08-01T00:00:00Z' }], activeProvider: 0, ...overrides };
  }

  function session(overrides: Partial<AiSessionView> = {}): AiSessionView {
    return {
      id: 'session-1',
      from: '2026-08-01',
      to: '2026-08-03',
      requestedSlots: [2],
      status: 0,
      transcript: [],
      draft: [],
      ...overrides
    };
  }

  function notFound(): HttpErrorResponse {
    return new HttpErrorResponse({ status: 404, statusText: 'Not Found' });
  }

  interface Stubs {
    guardians?: Partial<GuardiansService>;
    aiAssistant?: Partial<AiAssistantService>;
  }

  async function setup(stubs: Stubs = {}) {
    const guardiansStub: Partial<GuardiansService> = {
      listMyChildren: vi.fn(async () => [child()]),
      ...stubs.guardians
    };
    const aiAssistantStub: Partial<AiAssistantService> = {
      listProviders: vi.fn(async () => providerSettings()),
      getCurrentSession: vi.fn(async () => Promise.reject(notFound())),
      startSession: vi.fn(async () => session()),
      sendMessage: vi.fn(async () => session()),
      applyDraft: vi.fn(async () => session({ status: 1 })),
      discardSession: vi.fn(async () => session({ status: 2 })),
      ...stubs.aiAssistant
    };

    await TestBed.configureTestingModule({
      imports: [MealplanAiAssistant],
      providers: [
        provideRouter([]),
        { provide: GuardiansService, useValue: guardiansStub },
        { provide: AiAssistantService, useValue: aiAssistantStub }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(MealplanAiAssistant);

    return { fixture, guardians: guardiansStub, aiAssistant: aiAssistantStub };
  }

  async function settle(fixture: { detectChanges: () => void; whenStable: () => Promise<boolean> }) {
    fixture.detectChanges();

    for (let i = 0; i < 10; i++) {
      await fixture.whenStable();
      fixture.detectChanges();
    }
  }

  function findButtonByText(compiled: HTMLElement, text: string): HTMLButtonElement | undefined {
    return Array.from(compiled.querySelectorAll('button')).find((button) => button.textContent?.trim() === text);
  }

  it('shows a hint when the guardian has no linked children', async () => {
    const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => []) } });
    await settle(fixture);

    expect(fixture.nativeElement.textContent).toContain('Link a child from Settings before using the AI assistant.');
  });

  it('prompts to configure a provider before showing the start form', async () => {
    const { fixture } = await setup({ aiAssistant: { listProviders: vi.fn(async () => providerSettings({ activeProvider: null })) } });
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    expect(compiled.textContent).toContain('Add an AI provider API key in Settings before starting a session.');
    expect(compiled.querySelector('form')).toBeNull();
  });

  it('shows the start form with a default slot selected when there is no current session', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    expect(compiled.textContent).toContain('Start a new session');
    expect(findButtonByText(compiled, 'Dinner')).toBeTruthy();
  });

  it('restores an in-progress session on load', async () => {
    const { fixture } = await setup({ aiAssistant: { getCurrentSession: vi.fn(async () => session()) } });
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
      expect.objectContaining({ slots: [2], mustIncludeMealIds: [], notes: null })
    );
    expect(fixture.nativeElement.textContent).toContain('Drafting');
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
          session({ transcript: [{ role: 0, text: 'Plan five dinners.', occurredAt: '2026-08-01T00:00:00Z' }] })
        )
      }
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
          session({ draft: [{ date: '2026-08-01', slot: 2, mealId: 'meal-1', mealName: 'Tacos' }] })
        )
      }
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
    const { fixture, aiAssistant } = await setup({ aiAssistant: { getCurrentSession: vi.fn(async () => session()) } });
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

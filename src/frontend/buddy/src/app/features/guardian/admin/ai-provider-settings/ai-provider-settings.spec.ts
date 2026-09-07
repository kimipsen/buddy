import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { AiAssistantService, AiProviderSettings, TestProviderConnectionResult } from '../../../../core/ai-assistant.service';
import { ChildSummary, GuardiansService } from '../../../../core/guardians.service';
import { AiProviderSettingsComponent } from './ai-provider-settings';

describe('AiProviderSettingsComponent', () => {
  function child(overrides: Partial<ChildSummary> = {}): ChildSummary {
    return { id: 'child-1', name: { givenName: 'Alex', familyName: 'Doe' }, guardianLinkId: 'link-1', kind: 0, language: 'en', timeZoneId: 'UTC', ...overrides };
  }

  function settings(overrides: Partial<AiProviderSettings> = {}): AiProviderSettings {
    return { providers: [], activeProvider: null, ...overrides };
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
      listProviders: vi.fn(async () => settings()),
      setProviderApiKey: vi.fn(async () => settings({ providers: [{ provider: 0, last4: 'test', addedAt: '2026-08-01T00:00:00Z' }], activeProvider: 0 })),
      removeProviderApiKey: vi.fn(async () => settings()),
      setActiveProvider: vi.fn(async () => settings({ activeProvider: 1 })),
      testProviderConnection: vi.fn(async () => ({ isSuccessful: true, errorMessage: null }) as TestProviderConnectionResult),
      ...stubs.aiAssistant
    };

    await TestBed.configureTestingModule({
      imports: [AiProviderSettingsComponent],
      providers: [
        { provide: GuardiansService, useValue: guardiansStub },
        { provide: AiAssistantService, useValue: aiAssistantStub }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(AiProviderSettingsComponent);

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

    expect(fixture.nativeElement.textContent).toContain('Link a child from Settings before configuring an AI provider.');
  });

  it('shows an error when providers fail to load', async () => {
    const { fixture } = await setup({ aiAssistant: { listProviders: vi.fn(async () => Promise.reject(new Error('boom'))) } });
    await settle(fixture);

    expect(fixture.nativeElement.textContent).toContain('Unable to load AI provider settings.');
  });

  it('lists all three providers, none configured', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    expect(compiled.textContent).toContain('Anthropic (Claude)');
    expect(compiled.textContent).toContain('OpenAI (ChatGPT)');
    expect(compiled.textContent).toContain('Google (Gemini)');
    expect(Array.from(compiled.querySelectorAll('li')).filter((li) => li.textContent?.includes('Add key'))).toHaveLength(3);
  });

  it('saves a new API key and shows the configured provider as active', async () => {
    const { fixture, aiAssistant } = await setup();
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    findButtonByText(compiled, 'Add key')!.click();
    await settle(fixture);

    const input = compiled.querySelector<HTMLInputElement>('input[name="apiKey"]')!;
    input.value = 'sk-ant-test1234';
    input.dispatchEvent(new Event('input'));
    await settle(fixture);

    findButtonByText(compiled, 'Save')!.click();
    await settle(fixture);

    expect(aiAssistant.setProviderApiKey).toHaveBeenCalledWith('child-1', 0, 'sk-ant-test1234');
    expect(compiled.textContent).toContain('Active');
    expect(compiled.textContent).toContain('••••test');
  });

  it('shows a save error when adding a key fails', async () => {
    const { fixture } = await setup({ aiAssistant: { setProviderApiKey: vi.fn(async () => Promise.reject(new Error('boom'))) } });
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    findButtonByText(compiled, 'Add key')!.click();
    await settle(fixture);

    const input = compiled.querySelector<HTMLInputElement>('input[name="apiKey"]')!;
    input.value = 'sk-ant-test1234';
    input.dispatchEvent(new Event('input'));
    await settle(fixture);

    findButtonByText(compiled, 'Save')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Unable to save this API key.');
  });

  it('tests a configured provider connection and shows the result', async () => {
    const configured = settings({ providers: [{ provider: 0, last4: '1234', addedAt: '2026-08-01T00:00:00Z' }], activeProvider: 0 });
    const { fixture, aiAssistant } = await setup({
      aiAssistant: {
        listProviders: vi.fn(async () => configured),
        testProviderConnection: vi.fn(async () => ({ isSuccessful: false, errorMessage: 'Incorrect API key provided.' }))
      }
    });
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    findButtonByText(compiled, 'Test connection')!.click();
    await settle(fixture);

    expect(aiAssistant.testProviderConnection).toHaveBeenCalledWith('child-1', 0);
    expect(compiled.textContent).toContain('Connection failed.');
    expect(compiled.textContent).toContain('Incorrect API key provided.');
  });

  it('makes a configured provider active', async () => {
    const configured = settings({
      providers: [
        { provider: 0, last4: '1111', addedAt: '2026-08-01T00:00:00Z' },
        { provider: 1, last4: '2222', addedAt: '2026-08-01T00:00:00Z' }
      ],
      activeProvider: 0
    });
    const { fixture, aiAssistant } = await setup({ aiAssistant: { listProviders: vi.fn(async () => configured) } });
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    findButtonByText(compiled, 'Make active')!.click();
    await settle(fixture);

    expect(aiAssistant.setActiveProvider).toHaveBeenCalledWith('child-1', 1);
    expect(compiled.textContent).toContain('Active');
  });

  it('removes a configured key after confirming', async () => {
    const configured = settings({ providers: [{ provider: 0, last4: '1234', addedAt: '2026-08-01T00:00:00Z' }], activeProvider: 0 });
    const { fixture, aiAssistant } = await setup({ aiAssistant: { listProviders: vi.fn(async () => configured) } });
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    findButtonByText(compiled, 'Remove')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Remove this API key?');

    findButtonByText(compiled, 'Confirm')!.click();
    await settle(fixture);

    expect(aiAssistant.removeProviderApiKey).toHaveBeenCalledWith('child-1', 0);
    expect(compiled.textContent).toContain('Add key');
  });
});

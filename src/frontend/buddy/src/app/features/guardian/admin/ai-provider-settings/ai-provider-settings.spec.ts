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

  function deferred<T>(): { promise: Promise<T>; resolve: (value: T) => void; reject: (reason?: unknown) => void } {
    let resolve!: (value: T) => void;
    let reject!: (reason?: unknown) => void;
    const promise = new Promise<T>((res, rej) => {
      resolve = res;
      reject = rej;
    });
    return { promise, resolve, reject };
  }

  function row(compiled: HTMLElement, provider: 0 | 1 | 2): HTMLElement {
    return compiled.querySelectorAll<HTMLElement>('li')[provider];
  }

  function rowButton(compiled: HTMLElement, provider: 0 | 1 | 2, text: string): HTMLButtonElement | undefined {
    return findButtonByText(row(compiled, provider), text);
  }

  async function typeKey(fixture: { nativeElement: HTMLElement; detectChanges: () => void; whenStable: () => Promise<boolean> }, value: string) {
    const input = fixture.nativeElement.querySelector<HTMLInputElement>('input[name="apiKey"]')!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    await settle(fixture);
  }

  const twoConfigured = () =>
    settings({
      providers: [
        { provider: 0, last4: '1111', addedAt: '2026-08-01T00:00:00Z' },
        { provider: 1, last4: '2222', addedAt: '2026-08-01T00:00:00Z' }
      ],
      activeProvider: 0
    });

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

  it('shows the loading hint until children have loaded', async () => {
    const children = deferred<ChildSummary[]>();
    const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(() => children.promise) } });
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    expect(compiled.textContent).toContain('Loading providers…');
    expect(compiled.querySelectorAll('li')).toHaveLength(0);

    children.resolve([child()]);
    await settle(fixture);

    expect(compiled.textContent).not.toContain('Loading providers…');
    expect(compiled.querySelectorAll('li')).toHaveLength(3);
  });

  it('shows the load error (not the no-children hint) when listing children fails', async () => {
    const { fixture, aiAssistant } = await setup({ guardians: { listMyChildren: vi.fn(async () => Promise.reject(new Error('boom'))) } });
    await settle(fixture);

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Unable to load AI provider settings.');
    expect(text).not.toContain('Link a child from Settings before configuring an AI provider.');
    expect(aiAssistant.listProviders).not.toHaveBeenCalled();
  });

  it('toggles the key editor closed when the same provider is clicked again', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    rowButton(compiled, 0, 'Add key')!.click();
    await settle(fixture);

    expect(row(compiled, 0).querySelector('input[name="apiKey"]')).not.toBeNull();
    expect(rowButton(compiled, 0, 'Add key')).toBeUndefined();

    rowButton(compiled, 0, 'Close')!.click();
    await settle(fixture);

    expect(compiled.querySelector('input[name="apiKey"]')).toBeNull();
    expect(rowButton(compiled, 0, 'Add key')).toBeDefined();
  });

  it('resets the typed key and previous save error when the editor is reopened', async () => {
    const { fixture } = await setup({ aiAssistant: { setProviderApiKey: vi.fn(async () => Promise.reject(new Error('boom'))) } });
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    rowButton(compiled, 0, 'Add key')!.click();
    await settle(fixture);
    await typeKey(fixture, 'sk-ant-test1234');

    findButtonByText(compiled, 'Save')!.click();
    await settle(fixture);
    expect(compiled.textContent).toContain('Unable to save this API key.');

    rowButton(compiled, 0, 'Close')!.click();
    await settle(fixture);
    rowButton(compiled, 0, 'Add key')!.click();
    await settle(fixture);

    const input = compiled.querySelector<HTMLInputElement>('input[name="apiKey"]')!;
    expect(input.value).toBe('');
    expect(findButtonByText(compiled, 'Save')!.disabled).toBe(true);
    expect(compiled.textContent).not.toContain('Unable to save this API key.');
  });

  it('trims the API key and closes the editor after a successful save', async () => {
    const { fixture, aiAssistant } = await setup();
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    rowButton(compiled, 1, 'Add key')!.click();
    await settle(fixture);
    await typeKey(fixture, '   sk-openai-5678  ');

    findButtonByText(compiled, 'Save')!.click();
    await settle(fixture);

    expect(aiAssistant.setProviderApiKey).toHaveBeenCalledWith('child-1', 1, 'sk-openai-5678');
    expect(compiled.querySelector('input[name="apiKey"]')).toBeNull();
    expect(rowButton(compiled, 1, 'Close')).toBeUndefined();
  });

  it('disables Save while saving, clears the previous error on retry and re-enables it after a failure', async () => {
    const attempts: ReturnType<typeof deferred<AiProviderSettings>>[] = [];
    const setProviderApiKey = vi.fn(() => {
      const next = deferred<AiProviderSettings>();
      attempts.push(next);
      return next.promise;
    });
    const { fixture } = await setup({ aiAssistant: { setProviderApiKey } });
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    rowButton(compiled, 0, 'Add key')!.click();
    await settle(fixture);
    await typeKey(fixture, 'sk-ant-test1234');

    findButtonByText(compiled, 'Save')!.click();
    await settle(fixture);
    expect(findButtonByText(compiled, 'Save')!.disabled).toBe(true);

    attempts[0].reject(new Error('boom'));
    await settle(fixture);
    expect(compiled.textContent).toContain('Unable to save this API key.');
    expect(findButtonByText(compiled, 'Save')!.disabled).toBe(false);

    findButtonByText(compiled, 'Save')!.click();
    await settle(fixture);
    expect(setProviderApiKey).toHaveBeenCalledTimes(2);
    expect(compiled.textContent).not.toContain('Unable to save this API key.');
    expect(findButtonByText(compiled, 'Save')!.disabled).toBe(true);

    attempts[1].reject(new Error('boom'));
    await settle(fixture);
    expect(compiled.textContent).toContain('Unable to save this API key.');
    expect(findButtonByText(compiled, 'Save')!.disabled).toBe(false);
  });

  it('closes an open editor when removal is requested and hides the prompt on cancel', async () => {
    const { fixture, aiAssistant } = await setup({ aiAssistant: { listProviders: vi.fn(async () => twoConfigured()) } });
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    rowButton(compiled, 0, 'Replace key')!.click();
    await settle(fixture);
    expect(compiled.querySelector('input[name="apiKey"]')).not.toBeNull();

    rowButton(compiled, 0, 'Remove')!.click();
    await settle(fixture);
    expect(compiled.querySelector('input[name="apiKey"]')).toBeNull();
    expect(row(compiled, 0).textContent).toContain('Remove this API key?');

    rowButton(compiled, 0, 'Cancel')!.click();
    await settle(fixture);
    expect(compiled.textContent).not.toContain('Remove this API key?');
    expect(rowButton(compiled, 0, 'Replace key')).toBeDefined();
    expect(aiAssistant.removeProviderApiKey).not.toHaveBeenCalled();
  });

  it('dismisses a pending remove confirmation when editing another provider', async () => {
    const { fixture } = await setup({ aiAssistant: { listProviders: vi.fn(async () => twoConfigured()) } });
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    rowButton(compiled, 0, 'Remove')!.click();
    await settle(fixture);
    expect(compiled.textContent).toContain('Remove this API key?');

    rowButton(compiled, 1, 'Replace key')!.click();
    await settle(fixture);

    expect(compiled.textContent).not.toContain('Remove this API key?');
    expect(row(compiled, 1).querySelector('input[name="apiKey"]')).not.toBeNull();
  });

  it('applies the returned settings and closes the prompt after removing', async () => {
    const { fixture } = await setup({
      aiAssistant: {
        listProviders: vi.fn(async () => twoConfigured()),
        removeProviderApiKey: vi.fn(async () => settings({ providers: [{ provider: 1, last4: '2222', addedAt: '2026-08-01T00:00:00Z' }], activeProvider: 1 }))
      }
    });
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    rowButton(compiled, 0, 'Remove')!.click();
    await settle(fixture);
    rowButton(compiled, 0, 'Confirm')!.click();
    await settle(fixture);

    expect(compiled.textContent).not.toContain('Remove this API key?');
    expect(row(compiled, 0).textContent).not.toContain('••••1111');
    expect(rowButton(compiled, 0, 'Add key')).toBeDefined();
    expect(row(compiled, 1).textContent).toContain('Active');
  });

  it('disables the confirm buttons while removing, shows an error on failure and clears it on retry', async () => {
    const attempts: ReturnType<typeof deferred<AiProviderSettings>>[] = [];
    const removeProviderApiKey = vi.fn(() => {
      const next = deferred<AiProviderSettings>();
      attempts.push(next);
      return next.promise;
    });
    const { fixture } = await setup({ aiAssistant: { listProviders: vi.fn(async () => twoConfigured()), removeProviderApiKey } });
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    rowButton(compiled, 0, 'Remove')!.click();
    await settle(fixture);
    expect(rowButton(compiled, 0, 'Confirm')!.disabled).toBe(false);

    rowButton(compiled, 0, 'Confirm')!.click();
    await settle(fixture);
    expect(rowButton(compiled, 0, 'Confirm')!.disabled).toBe(true);
    expect(rowButton(compiled, 0, 'Cancel')!.disabled).toBe(true);

    attempts[0].reject(new Error('boom'));
    await settle(fixture);
    expect(compiled.textContent).toContain('Unable to remove this API key.');
    expect(row(compiled, 0).textContent).toContain('Remove this API key?');
    expect(rowButton(compiled, 0, 'Confirm')!.disabled).toBe(false);
    expect(rowButton(compiled, 0, 'Cancel')!.disabled).toBe(false);

    rowButton(compiled, 0, 'Confirm')!.click();
    await settle(fixture);
    expect(removeProviderApiKey).toHaveBeenCalledTimes(2);
    expect(compiled.textContent).not.toContain('Unable to remove this API key.');

    attempts[1].reject(new Error('boom'));
    await settle(fixture);
    expect(compiled.textContent).toContain('Unable to remove this API key.');
  });

  it('clears a previous remove error when removal is requested again', async () => {
    const { fixture } = await setup({
      aiAssistant: { listProviders: vi.fn(async () => twoConfigured()), removeProviderApiKey: vi.fn(async () => Promise.reject(new Error('boom'))) }
    });
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    rowButton(compiled, 0, 'Remove')!.click();
    await settle(fixture);
    rowButton(compiled, 0, 'Confirm')!.click();
    await settle(fixture);
    rowButton(compiled, 0, 'Cancel')!.click();
    await settle(fixture);
    expect(compiled.textContent).toContain('Unable to remove this API key.');

    rowButton(compiled, 0, 'Remove')!.click();
    await settle(fixture);
    expect(compiled.textContent).not.toContain('Unable to remove this API key.');
  });

  it('moves the active badge to the provider returned after making it active', async () => {
    const { fixture } = await setup({
      aiAssistant: {
        listProviders: vi.fn(async () => twoConfigured()),
        setActiveProvider: vi.fn(async () => ({ ...twoConfigured(), activeProvider: 1 as const }))
      }
    });
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    expect(row(compiled, 0).textContent).toContain('Active');
    expect(row(compiled, 1).textContent).not.toContain('Active');

    rowButton(compiled, 1, 'Make active')!.click();
    await settle(fixture);

    expect(row(compiled, 1).textContent).toContain('Active');
    expect(rowButton(compiled, 1, 'Make active')).toBeUndefined();
    expect(rowButton(compiled, 0, 'Make active')).toBeDefined();
  });

  it('disables Make active while switching, shows an error on failure and clears it on retry', async () => {
    const attempts: ReturnType<typeof deferred<AiProviderSettings>>[] = [];
    const setActiveProvider = vi.fn(() => {
      const next = deferred<AiProviderSettings>();
      attempts.push(next);
      return next.promise;
    });
    const { fixture } = await setup({ aiAssistant: { listProviders: vi.fn(async () => twoConfigured()), setActiveProvider } });
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    expect(rowButton(compiled, 1, 'Make active')!.disabled).toBe(false);

    rowButton(compiled, 1, 'Make active')!.click();
    await settle(fixture);
    expect(rowButton(compiled, 1, 'Make active')!.disabled).toBe(true);

    attempts[0].reject(new Error('boom'));
    await settle(fixture);
    expect(compiled.textContent).toContain('Unable to switch the active provider.');
    expect(rowButton(compiled, 1, 'Make active')!.disabled).toBe(false);
    expect(row(compiled, 0).textContent).toContain('Active');

    rowButton(compiled, 1, 'Make active')!.click();
    await settle(fixture);
    expect(setActiveProvider).toHaveBeenCalledTimes(2);
    expect(compiled.textContent).not.toContain('Unable to switch the active provider.');

    attempts[1].reject(new Error('boom'));
    await settle(fixture);
    expect(compiled.textContent).toContain('Unable to switch the active provider.');
  });

  it('shows the testing state while a connection test is in flight and resets it after a failure', async () => {
    const pending = deferred<TestProviderConnectionResult>();
    const { fixture } = await setup({
      aiAssistant: { listProviders: vi.fn(async () => twoConfigured()), testProviderConnection: vi.fn(() => pending.promise) }
    });
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    rowButton(compiled, 0, 'Test connection')!.click();
    await settle(fixture);

    expect(rowButton(compiled, 0, 'Test connection')).toBeUndefined();
    expect(rowButton(compiled, 0, 'Testing…')!.disabled).toBe(true);
    expect(rowButton(compiled, 1, 'Test connection')!.disabled).toBe(false);

    pending.reject(new Error('boom'));
    await settle(fixture);

    expect(rowButton(compiled, 0, 'Testing…')).toBeUndefined();
    expect(rowButton(compiled, 0, 'Test connection')!.disabled).toBe(false);
    expect(row(compiled, 0).textContent).toContain('Connection failed.');
    expect(row(compiled, 0).textContent).not.toContain('Connection succeeded.');
    expect(row(compiled, 0).textContent).not.toContain('—');
  });

  it('clears the previous test result when the editor is opened for that provider', async () => {
    const { fixture } = await setup({ aiAssistant: { listProviders: vi.fn(async () => twoConfigured()) } });
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    rowButton(compiled, 0, 'Test connection')!.click();
    await settle(fixture);
    rowButton(compiled, 1, 'Test connection')!.click();
    await settle(fixture);
    expect(row(compiled, 0).textContent).toContain('Connection succeeded.');
    expect(row(compiled, 1).textContent).toContain('Connection succeeded.');

    rowButton(compiled, 0, 'Replace key')!.click();
    await settle(fixture);

    expect(row(compiled, 0).textContent).not.toContain('Connection succeeded.');
    expect(row(compiled, 1).textContent).toContain('Connection succeeded.');
  });
});

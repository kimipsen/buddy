import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import {
  AiAssistantService,
  AiProvider,
  AiProviderSettings,
  TestProviderConnectionResult,
} from '../../../../core/ai-assistant.service';
import { ChildSummary, GuardiansService } from '../../../../core/guardians.service';
import { AiProviderSettingsComponent } from './ai-provider-settings';

describe('AiProviderSettingsComponent', () => {
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

  function settings(overrides: Partial<AiProviderSettings> = {}): AiProviderSettings {
    return { providers: [], activeProvider: null, dataSharingAcknowledgedAt: null, ...overrides };
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
      listProviders: vi.fn(async () => settings()),
      setProviderApiKey: vi.fn(async () =>
        settings({
          providers: [{ provider: 'Anthropic', last4: 'test', addedAt: '2026-08-01T00:00:00Z' }],
          activeProvider: 'Anthropic',
        }),
      ),
      removeProviderApiKey: vi.fn(async () => settings()),
      setActiveProvider: vi.fn(async () => settings({ activeProvider: 'OpenAi' })),
      testProviderConnection: vi.fn(async (): Promise<TestProviderConnectionResult> => ({
        kind: 0,
      })),
      ...stubs.aiAssistant,
    };

    await TestBed.configureTestingModule({
      imports: [AiProviderSettingsComponent],
      providers: [
        { provide: GuardiansService, useValue: guardiansStub },
        { provide: AiAssistantService, useValue: aiAssistantStub },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(AiProviderSettingsComponent);

    return { fixture, guardians: guardiansStub, aiAssistant: aiAssistantStub };
  }

  // The settings load in a resource(), which whenStable() waits on, so a test that holds the load
  // open would never settle. Flush macrotasks instead, repeated to cover chained awaits.
  async function settle(fixture: { detectChanges: () => void }) {
    fixture.detectChanges();

    for (let i = 0; i < 10; i++) {
      await new Promise((resolve) => setTimeout(resolve, 0));
      fixture.detectChanges();
    }
  }

  function findButtonByText(compiled: HTMLElement, text: string): HTMLButtonElement | undefined {
    return Array.from(compiled.querySelectorAll('button')).find(
      (button) => button.textContent?.trim() === text,
    );
  }

  function deferred<T>(): {
    promise: Promise<T>;
    resolve: (value: T) => void;
    reject: (reason?: unknown) => void;
  } {
    let resolve!: (value: T) => void;
    let reject!: (reason?: unknown) => void;
    const promise = new Promise<T>((res, rej) => {
      resolve = res;
      reject = rej;
    });
    return { promise, resolve, reject };
  }

  // Rows render in PROVIDERS order (alphabetical by display name: Anthropic, Google/Gemini,
  // OpenAI), not in the AiProvider enum's declaration order.
  const ROW_POSITION: Record<AiProvider, number> = { Anthropic: 0, Gemini: 1, OpenAi: 2 };

  // The data-sharing notice below the provider list has list items of its own.
  function providerRows(compiled: HTMLElement): HTMLElement[] {
    return Array.from(compiled.querySelectorAll<HTMLElement>('li')).filter(
      (li) => !li.closest('app-ai-data-sharing-notice'),
    );
  }

  function row(compiled: HTMLElement, provider: AiProvider): HTMLElement {
    return providerRows(compiled)[ROW_POSITION[provider]];
  }

  function rowButton(
    compiled: HTMLElement,
    provider: AiProvider,
    text: string,
  ): HTMLButtonElement | undefined {
    return findButtonByText(row(compiled, provider), text);
  }

  async function typeKey(
    fixture: {
      nativeElement: HTMLElement;
      detectChanges: () => void;
    },
    value: string,
  ) {
    const input = fixture.nativeElement.querySelector<HTMLInputElement>('input[name="apiKey"]')!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    await settle(fixture);
  }

  const twoConfigured = () =>
    settings({
      providers: [
        { provider: 'Anthropic', last4: '1111', addedAt: '2026-08-01T00:00:00Z' },
        { provider: 'OpenAi', last4: '2222', addedAt: '2026-08-01T00:00:00Z' },
      ],
      activeProvider: 'Anthropic',
    });

  it('shows a hint when the guardian has no linked children', async () => {
    const { fixture } = await setup({ guardians: { listMyChildren: vi.fn(async () => []) } });
    await settle(fixture);

    expect(fixture.nativeElement.textContent).toContain(
      'Link a child from Settings before configuring an AI provider.',
    );
  });

  it('shows an error when providers fail to load', async () => {
    const { fixture } = await setup({
      aiAssistant: { listProviders: vi.fn(async () => Promise.reject(new Error('boom'))) },
    });
    await settle(fixture);

    expect(fixture.nativeElement.textContent).toContain('Unable to load AI provider settings.');
  });

  it('shows what the assistant shares, without an acknowledge button before a provider is active', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    expect(compiled.textContent).toContain('What the assistant shares');
    expect(findButtonByText(compiled, 'I understand, continue')).toBeUndefined();
  });

  it('lets a guardian acknowledge data sharing once a provider is active', async () => {
    const acknowledgeDataSharing = vi.fn(async () =>
      settings({ activeProvider: 'Anthropic', dataSharingAcknowledgedAt: '2026-08-02T09:30:00Z' }),
    );
    const { fixture } = await setup({
      aiAssistant: {
        listProviders: vi.fn(async () => settings({ activeProvider: 'Anthropic' })),
        acknowledgeDataSharing,
      },
    });
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    findButtonByText(compiled, 'I understand, continue')!.click();
    await settle(fixture);

    expect(acknowledgeDataSharing).toHaveBeenCalledWith('child-1');
    expect(compiled.textContent).toContain('Acknowledged on 2026-08-02.');
    expect(findButtonByText(compiled, 'I understand, continue')).toBeUndefined();
  });

  it('lists all three providers, none configured', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    expect(compiled.textContent).toContain('Anthropic (Claude)');
    expect(compiled.textContent).toContain('OpenAI (ChatGPT)');
    expect(compiled.textContent).toContain('Google (Gemini)');
    expect(providerRows(compiled).filter((li) => li.textContent?.includes('Add key'))).toHaveLength(
      3,
    );
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

    expect(aiAssistant.setProviderApiKey).toHaveBeenCalledWith(
      'child-1',
      'Anthropic',
      'sk-ant-test1234',
    );
    expect(compiled.textContent).toContain('Active');
    expect(compiled.textContent).toContain('••••test');
  });

  it('shows a save error when adding a key fails', async () => {
    const { fixture } = await setup({
      aiAssistant: { setProviderApiKey: vi.fn(async () => Promise.reject(new Error('boom'))) },
    });
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
    const configured = settings({
      providers: [{ provider: 'Anthropic', last4: '1234', addedAt: '2026-08-01T00:00:00Z' }],
      activeProvider: 'Anthropic',
    });
    const { fixture, aiAssistant } = await setup({
      aiAssistant: {
        listProviders: vi.fn(async () => configured),
        testProviderConnection: vi.fn(async (): Promise<TestProviderConnectionResult> => ({
          kind: 1,
          message: 'Incorrect API key provided.',
        })),
      },
    });
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    findButtonByText(compiled, 'Test connection')!.click();
    await settle(fixture);

    expect(aiAssistant.testProviderConnection).toHaveBeenCalledWith('child-1', 'Anthropic');
    expect(compiled.textContent).toContain('Connection failed.');
    expect(compiled.textContent).toContain('Incorrect API key provided.');
  });

  it('makes a configured provider active', async () => {
    const configured = settings({
      providers: [
        { provider: 'Anthropic', last4: '1111', addedAt: '2026-08-01T00:00:00Z' },
        { provider: 'OpenAi', last4: '2222', addedAt: '2026-08-01T00:00:00Z' },
      ],
      activeProvider: 'Anthropic',
    });
    const { fixture, aiAssistant } = await setup({
      aiAssistant: { listProviders: vi.fn(async () => configured) },
    });
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    findButtonByText(compiled, 'Make active')!.click();
    await settle(fixture);

    expect(aiAssistant.setActiveProvider).toHaveBeenCalledWith('child-1', 'OpenAi');
    expect(compiled.textContent).toContain('Active');
  });

  it('removes a configured key after confirming', async () => {
    const configured = settings({
      providers: [{ provider: 'Anthropic', last4: '1234', addedAt: '2026-08-01T00:00:00Z' }],
      activeProvider: 'Anthropic',
    });
    const { fixture, aiAssistant } = await setup({
      aiAssistant: { listProviders: vi.fn(async () => configured) },
    });
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    findButtonByText(compiled, 'Remove')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Remove this API key?');

    findButtonByText(compiled, 'Confirm')!.click();
    await settle(fixture);

    expect(aiAssistant.removeProviderApiKey).toHaveBeenCalledWith('child-1', 'Anthropic');
    expect(compiled.textContent).toContain('Add key');
  });

  it('shows the loading hint until children have loaded', async () => {
    const children = deferred<ChildSummary[]>();
    const { fixture } = await setup({
      guardians: { listMyChildren: vi.fn(() => children.promise) },
    });
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    expect(compiled.textContent).toContain('Loading providers…');
    expect(providerRows(compiled)).toHaveLength(0);

    children.resolve([child()]);
    await settle(fixture);

    expect(compiled.textContent).not.toContain('Loading providers…');
    expect(providerRows(compiled)).toHaveLength(3);
  });

  it('shows the load error (not the no-children hint) when listing children fails', async () => {
    const { fixture, aiAssistant } = await setup({
      guardians: { listMyChildren: vi.fn(async () => Promise.reject(new Error('boom'))) },
    });
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
    rowButton(compiled, 'Anthropic', 'Add key')!.click();
    await settle(fixture);

    expect(row(compiled, 'Anthropic').querySelector('input[name="apiKey"]')).not.toBeNull();
    expect(rowButton(compiled, 'Anthropic', 'Add key')).toBeUndefined();

    rowButton(compiled, 'Anthropic', 'Close')!.click();
    await settle(fixture);

    expect(compiled.querySelector('input[name="apiKey"]')).toBeNull();
    expect(rowButton(compiled, 'Anthropic', 'Add key')).toBeDefined();
  });

  it('resets the typed key and previous save error when the editor is reopened', async () => {
    const { fixture } = await setup({
      aiAssistant: { setProviderApiKey: vi.fn(async () => Promise.reject(new Error('boom'))) },
    });
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    rowButton(compiled, 'Anthropic', 'Add key')!.click();
    await settle(fixture);
    await typeKey(fixture, 'sk-ant-test1234');

    findButtonByText(compiled, 'Save')!.click();
    await settle(fixture);
    expect(compiled.textContent).toContain('Unable to save this API key.');

    rowButton(compiled, 'Anthropic', 'Close')!.click();
    await settle(fixture);
    rowButton(compiled, 'Anthropic', 'Add key')!.click();
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
    rowButton(compiled, 'OpenAi', 'Add key')!.click();
    await settle(fixture);
    await typeKey(fixture, '   sk-openai-5678  ');

    findButtonByText(compiled, 'Save')!.click();
    await settle(fixture);

    expect(aiAssistant.setProviderApiKey).toHaveBeenCalledWith(
      'child-1',
      'OpenAi',
      'sk-openai-5678',
    );
    expect(compiled.querySelector('input[name="apiKey"]')).toBeNull();
    expect(rowButton(compiled, 'OpenAi', 'Close')).toBeUndefined();
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
    rowButton(compiled, 'Anthropic', 'Add key')!.click();
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
    const { fixture, aiAssistant } = await setup({
      aiAssistant: { listProviders: vi.fn(async () => twoConfigured()) },
    });
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    rowButton(compiled, 'Anthropic', 'Replace key')!.click();
    await settle(fixture);
    expect(compiled.querySelector('input[name="apiKey"]')).not.toBeNull();

    rowButton(compiled, 'Anthropic', 'Remove')!.click();
    await settle(fixture);
    expect(compiled.querySelector('input[name="apiKey"]')).toBeNull();
    expect(row(compiled, 'Anthropic').textContent).toContain('Remove this API key?');

    rowButton(compiled, 'Anthropic', 'Cancel')!.click();
    await settle(fixture);
    expect(compiled.textContent).not.toContain('Remove this API key?');
    expect(rowButton(compiled, 'Anthropic', 'Replace key')).toBeDefined();
    expect(aiAssistant.removeProviderApiKey).not.toHaveBeenCalled();
  });

  it('dismisses a pending remove confirmation when editing another provider', async () => {
    const { fixture } = await setup({
      aiAssistant: { listProviders: vi.fn(async () => twoConfigured()) },
    });
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    rowButton(compiled, 'Anthropic', 'Remove')!.click();
    await settle(fixture);
    expect(compiled.textContent).toContain('Remove this API key?');

    rowButton(compiled, 'OpenAi', 'Replace key')!.click();
    await settle(fixture);

    expect(compiled.textContent).not.toContain('Remove this API key?');
    expect(row(compiled, 'OpenAi').querySelector('input[name="apiKey"]')).not.toBeNull();
  });

  it('applies the returned settings and closes the prompt after removing', async () => {
    const { fixture } = await setup({
      aiAssistant: {
        listProviders: vi.fn(async () => twoConfigured()),
        removeProviderApiKey: vi.fn(async () =>
          settings({
            providers: [{ provider: 'OpenAi', last4: '2222', addedAt: '2026-08-01T00:00:00Z' }],
            activeProvider: 'OpenAi',
          }),
        ),
      },
    });
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    rowButton(compiled, 'Anthropic', 'Remove')!.click();
    await settle(fixture);
    rowButton(compiled, 'Anthropic', 'Confirm')!.click();
    await settle(fixture);

    expect(compiled.textContent).not.toContain('Remove this API key?');
    expect(row(compiled, 'Anthropic').textContent).not.toContain('••••1111');
    expect(rowButton(compiled, 'Anthropic', 'Add key')).toBeDefined();
    expect(row(compiled, 'OpenAi').textContent).toContain('Active');
  });

  it('disables the confirm buttons while removing, shows an error on failure and clears it on retry', async () => {
    const attempts: ReturnType<typeof deferred<AiProviderSettings>>[] = [];
    const removeProviderApiKey = vi.fn(() => {
      const next = deferred<AiProviderSettings>();
      attempts.push(next);
      return next.promise;
    });
    const { fixture } = await setup({
      aiAssistant: { listProviders: vi.fn(async () => twoConfigured()), removeProviderApiKey },
    });
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    rowButton(compiled, 'Anthropic', 'Remove')!.click();
    await settle(fixture);
    expect(rowButton(compiled, 'Anthropic', 'Confirm')!.disabled).toBe(false);

    rowButton(compiled, 'Anthropic', 'Confirm')!.click();
    await settle(fixture);
    expect(rowButton(compiled, 'Anthropic', 'Confirm')!.disabled).toBe(true);
    expect(rowButton(compiled, 'Anthropic', 'Cancel')!.disabled).toBe(true);

    attempts[0].reject(new Error('boom'));
    await settle(fixture);
    expect(compiled.textContent).toContain('Unable to remove this API key.');
    expect(row(compiled, 'Anthropic').textContent).toContain('Remove this API key?');
    expect(rowButton(compiled, 'Anthropic', 'Confirm')!.disabled).toBe(false);
    expect(rowButton(compiled, 'Anthropic', 'Cancel')!.disabled).toBe(false);

    rowButton(compiled, 'Anthropic', 'Confirm')!.click();
    await settle(fixture);
    expect(removeProviderApiKey).toHaveBeenCalledTimes(2);
    expect(compiled.textContent).not.toContain('Unable to remove this API key.');

    attempts[1].reject(new Error('boom'));
    await settle(fixture);
    expect(compiled.textContent).toContain('Unable to remove this API key.');
  });

  it('clears a previous remove error when removal is requested again', async () => {
    const { fixture } = await setup({
      aiAssistant: {
        listProviders: vi.fn(async () => twoConfigured()),
        removeProviderApiKey: vi.fn(async () => Promise.reject(new Error('boom'))),
      },
    });
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    rowButton(compiled, 'Anthropic', 'Remove')!.click();
    await settle(fixture);
    rowButton(compiled, 'Anthropic', 'Confirm')!.click();
    await settle(fixture);
    rowButton(compiled, 'Anthropic', 'Cancel')!.click();
    await settle(fixture);
    expect(compiled.textContent).toContain('Unable to remove this API key.');

    rowButton(compiled, 'Anthropic', 'Remove')!.click();
    await settle(fixture);
    expect(compiled.textContent).not.toContain('Unable to remove this API key.');
  });

  it('moves the active badge to the provider returned after making it active', async () => {
    const { fixture } = await setup({
      aiAssistant: {
        listProviders: vi.fn(async () => twoConfigured()),
        setActiveProvider: vi.fn(async () => ({
          ...twoConfigured(),
          activeProvider: 'OpenAi' as const,
        })),
      },
    });
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    expect(row(compiled, 'Anthropic').textContent).toContain('Active');
    expect(row(compiled, 'OpenAi').textContent).not.toContain('Active');

    rowButton(compiled, 'OpenAi', 'Make active')!.click();
    await settle(fixture);

    expect(row(compiled, 'OpenAi').textContent).toContain('Active');
    expect(rowButton(compiled, 'OpenAi', 'Make active')).toBeUndefined();
    expect(rowButton(compiled, 'Anthropic', 'Make active')).toBeDefined();
  });

  it('disables Make active while switching, shows an error on failure and clears it on retry', async () => {
    const attempts: ReturnType<typeof deferred<AiProviderSettings>>[] = [];
    const setActiveProvider = vi.fn(() => {
      const next = deferred<AiProviderSettings>();
      attempts.push(next);
      return next.promise;
    });
    const { fixture } = await setup({
      aiAssistant: { listProviders: vi.fn(async () => twoConfigured()), setActiveProvider },
    });
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    expect(rowButton(compiled, 'OpenAi', 'Make active')!.disabled).toBe(false);

    rowButton(compiled, 'OpenAi', 'Make active')!.click();
    await settle(fixture);
    expect(rowButton(compiled, 'OpenAi', 'Make active')!.disabled).toBe(true);

    attempts[0].reject(new Error('boom'));
    await settle(fixture);
    expect(compiled.textContent).toContain('Unable to switch the active provider.');
    expect(rowButton(compiled, 'OpenAi', 'Make active')!.disabled).toBe(false);
    expect(row(compiled, 'Anthropic').textContent).toContain('Active');

    rowButton(compiled, 'OpenAi', 'Make active')!.click();
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
      aiAssistant: {
        listProviders: vi.fn(async () => twoConfigured()),
        testProviderConnection: vi.fn(() => pending.promise),
      },
    });
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    rowButton(compiled, 'Anthropic', 'Test connection')!.click();
    await settle(fixture);

    expect(rowButton(compiled, 'Anthropic', 'Test connection')).toBeUndefined();
    expect(rowButton(compiled, 'Anthropic', 'Testing…')!.disabled).toBe(true);
    expect(rowButton(compiled, 'OpenAi', 'Test connection')!.disabled).toBe(false);

    pending.reject(new Error('boom'));
    await settle(fixture);

    expect(rowButton(compiled, 'Anthropic', 'Testing…')).toBeUndefined();
    expect(rowButton(compiled, 'Anthropic', 'Test connection')!.disabled).toBe(false);
    expect(row(compiled, 'Anthropic').textContent).toContain('Connection failed.');
    expect(row(compiled, 'Anthropic').textContent).not.toContain('Connection succeeded.');
    expect(row(compiled, 'Anthropic').textContent).not.toContain('—');
  });

  it('clears the previous test result when the editor is opened for that provider', async () => {
    const { fixture } = await setup({
      aiAssistant: { listProviders: vi.fn(async () => twoConfigured()) },
    });
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    rowButton(compiled, 'Anthropic', 'Test connection')!.click();
    await settle(fixture);
    rowButton(compiled, 'OpenAi', 'Test connection')!.click();
    await settle(fixture);
    expect(row(compiled, 'Anthropic').textContent).toContain('Connection succeeded.');
    expect(row(compiled, 'OpenAi').textContent).toContain('Connection succeeded.');

    rowButton(compiled, 'Anthropic', 'Replace key')!.click();
    await settle(fixture);

    expect(row(compiled, 'Anthropic').textContent).not.toContain('Connection succeeded.');
    expect(row(compiled, 'OpenAi').textContent).toContain('Connection succeeded.');
  });
});

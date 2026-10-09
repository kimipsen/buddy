import { Component, inject, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import {
  AiAssistantService,
  AiProvider,
  AiProviderSettings as AiProviderSettingsData,
  AiProviderSettingsEntry,
  TestProviderConnectionResult,
} from '../../../../core/ai-assistant.service';
import { GuardiansService } from '../../../../core/guardians.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { createAction } from '../../../../shared/action-state/action-state';
import { AiDataSharingNotice } from '../../mealplan/ai-data-sharing-notice/ai-data-sharing-notice';
import { Card } from '../../../../shared/card/card';

// Ordered to match the alphabetical order of their translated display names ("Anthropic (Claude)",
// "Google (Gemini)", "OpenAI (ChatGPT)"), like the other admin lists.
const PROVIDERS: readonly AiProvider[] = ['Anthropic', 'Gemini', 'OpenAi'];

const PROVIDER_LABEL_KEYS: Record<AiProvider, string> = {
  Anthropic: 'admin.aiProviders.names.anthropic',
  OpenAi: 'admin.aiProviders.names.openAi',
  Gemini: 'admin.aiProviders.names.gemini',
};

// What the settings page shows after a test: the API's answer, or that the test request itself
// failed (no message to show).
type ConnectionTestOutcome = TestProviderConnectionResult | { kind: 'unreachable' };

// What the page loaded: no linked child to configure, or the first child's provider settings.
type LoadedSettings =
  | { hasChildren: false }
  | {
      hasChildren: true;
      childId: string;
      configured: Map<AiProvider, AiProviderSettingsEntry>;
      activeProvider: AiProvider | null;
      dataSharingAcknowledgedAt: string | null;
    };

@Component({
  selector: 'app-ai-provider-settings',
  imports: [FormsModule, TranslatePipe, AiDataSharingNotice, Card],
  templateUrl: './ai-provider-settings.html',
})
export class AiProviderSettingsComponent {
  private readonly guardians = inject(GuardiansService);
  private readonly aiAssistant = inject(AiAssistantService);

  protected readonly providerList = PROVIDERS;
  protected readonly providerLabelKeys = PROVIDER_LABEL_KEYS;

  protected readonly settings = resource({ loader: () => this.loadSettings() });

  protected readonly editingProvider = signal<AiProvider | null>(null);
  // Stryker disable next-line StringLiteral: the input is only rendered/submittable after startEdit(), which always resets it to ''
  protected readonly apiKeyInput = signal('');
  protected readonly saving = createAction<AiProvider>();

  protected readonly settingActive = createAction<AiProvider>();

  protected readonly confirmingRemoveProvider = signal<AiProvider | null>(null);
  protected readonly removing = createAction<AiProvider>();

  protected readonly testingProvider = signal<AiProvider | null>(null);
  protected readonly testResults = signal<Map<AiProvider, ConnectionTestOutcome>>(new Map());

  protected startEdit(provider: AiProvider): void {
    this.confirmingRemoveProvider.set(null);
    this.clearTestResult(provider);

    if (this.editingProvider() === provider) {
      this.editingProvider.set(null);
      return;
    }

    this.editingProvider.set(provider);
    this.apiKeyInput.set('');
    this.saving.clearError();
  }

  protected async saveKey(childId: string, provider: AiProvider): Promise<void> {
    const apiKey = this.apiKeyInput().trim();

    if (!apiKey) {
      return;
    }

    await this.saving.run(
      provider,
      async () => {
        const settings = await this.aiAssistant.setProviderApiKey(childId, provider, apiKey);
        this.applySettings(settings);
        this.editingProvider.set(null);
      },
      'admin.aiProviders.saveError',
    );
  }

  protected requestRemove(provider: AiProvider): void {
    this.editingProvider.set(null);
    this.removing.clearError();
    this.confirmingRemoveProvider.set(provider);
  }

  protected cancelRemove(): void {
    this.confirmingRemoveProvider.set(null);
  }

  protected async confirmRemove(childId: string, provider: AiProvider): Promise<void> {
    await this.removing.run(
      provider,
      async () => {
        const settings = await this.aiAssistant.removeProviderApiKey(childId, provider);
        this.applySettings(settings);
        this.confirmingRemoveProvider.set(null);
      },
      'admin.aiProviders.remove.error',
    );
  }

  protected async makeActive(childId: string, provider: AiProvider): Promise<void> {
    await this.settingActive.run(
      provider,
      async () => {
        const settings = await this.aiAssistant.setActiveProvider(childId, provider);
        this.applySettings(settings);
      },
      'admin.aiProviders.activeError',
    );
  }

  protected async testConnection(childId: string, provider: AiProvider): Promise<void> {
    this.testingProvider.set(provider);

    try {
      const result = await this.aiAssistant.testProviderConnection(childId, provider);
      this.setTestResult(provider, result);
    } catch {
      this.setTestResult(provider, { kind: 'unreachable' });
    } finally {
      this.testingProvider.set(null);
    }
  }

  private setTestResult(provider: AiProvider, result: ConnectionTestOutcome): void {
    this.testResults.update((current) => new Map(current).set(provider, result));
  }

  private clearTestResult(provider: AiProvider): void {
    this.testResults.update((current) => {
      const next = new Map(current);
      next.delete(provider);
      return next;
    });
  }

  protected applySettings(settings: AiProviderSettingsData): void {
    this.settings.update((current) =>
      current?.hasChildren ? { ...current, ...this.toProviders(settings) } : current,
    );
  }

  private toProviders(settings: AiProviderSettingsData) {
    return {
      configured: new Map(settings.providers.map((entry) => [entry.provider, entry])),
      activeProvider: settings.activeProvider,
      dataSharingAcknowledgedAt: settings.dataSharingAcknowledgedAt,
    };
  }

  private async loadSettings(): Promise<LoadedSettings> {
    const [firstChild] = await this.guardians.listMyChildren();

    if (!firstChild) {
      return { hasChildren: false };
    }

    const settings = await this.aiAssistant.listProviders(firstChild.id);

    return { hasChildren: true, childId: firstChild.id, ...this.toProviders(settings) };
  }
}

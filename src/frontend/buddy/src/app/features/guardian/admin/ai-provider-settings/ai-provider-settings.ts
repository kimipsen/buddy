import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import {
  AiAssistantService,
  AiProvider,
  AiProviderSettings as AiProviderSettingsData,
  AiProviderSettingsEntry,
  TestProviderConnectionResult
} from '../../../../core/ai-assistant.service';
import { GuardiansService } from '../../../../core/guardians.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

const PROVIDERS: readonly AiProvider[] = [0, 1, 2];

const PROVIDER_LABEL_KEYS: Record<AiProvider, string> = {
  0: 'admin.aiProviders.names.anthropic',
  1: 'admin.aiProviders.names.openAi',
  2: 'admin.aiProviders.names.gemini'
};

@Component({
  selector: 'app-ai-provider-settings',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './ai-provider-settings.html'
})
export class AiProviderSettingsComponent implements OnInit {
  private readonly guardians = inject(GuardiansService);
  private readonly aiAssistant = inject(AiAssistantService);

  protected readonly providerList = PROVIDERS;
  protected readonly providerLabelKeys = PROVIDER_LABEL_KEYS;

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly hasChildren = signal(true);

  protected readonly configured = signal<Map<AiProvider, AiProviderSettingsEntry>>(new Map());
  protected readonly activeProvider = signal<AiProvider | null>(null);

  protected readonly editingProvider = signal<AiProvider | null>(null);
  protected readonly apiKeyInput = signal('');
  protected readonly saving = signal(false);
  protected readonly saveError = signal<string | null>(null);

  protected readonly settingActiveProvider = signal<AiProvider | null>(null);
  protected readonly activeError = signal<string | null>(null);

  protected readonly confirmingRemoveProvider = signal<AiProvider | null>(null);
  protected readonly removing = signal(false);
  protected readonly removeError = signal<string | null>(null);

  protected readonly testingProvider = signal<AiProvider | null>(null);
  protected readonly testResults = signal<Map<AiProvider, TestProviderConnectionResult>>(new Map());

  private childId: string | null = null;

  ngOnInit(): void {
    void this.load();
  }

  protected isConfigured(provider: AiProvider): boolean {
    return this.configured().has(provider);
  }

  protected entryFor(provider: AiProvider): AiProviderSettingsEntry | undefined {
    return this.configured().get(provider);
  }

  protected startEdit(provider: AiProvider): void {
    this.confirmingRemoveProvider.set(null);
    this.clearTestResult(provider);

    if (this.editingProvider() === provider) {
      this.editingProvider.set(null);
      return;
    }

    this.editingProvider.set(provider);
    this.apiKeyInput.set('');
    this.saveError.set(null);
  }

  protected async saveKey(provider: AiProvider): Promise<void> {
    const childId = this.childId;
    const apiKey = this.apiKeyInput().trim();

    if (!childId || !apiKey) {
      return;
    }

    this.saving.set(true);
    this.saveError.set(null);

    try {
      const settings = await this.aiAssistant.setProviderApiKey(childId, provider, apiKey);
      this.applySettings(settings);
      this.editingProvider.set(null);
    } catch {
      this.saveError.set('admin.aiProviders.saveError');
    } finally {
      this.saving.set(false);
    }
  }

  protected requestRemove(provider: AiProvider): void {
    this.editingProvider.set(null);
    this.removeError.set(null);
    this.confirmingRemoveProvider.set(provider);
  }

  protected cancelRemove(): void {
    this.confirmingRemoveProvider.set(null);
  }

  protected async confirmRemove(provider: AiProvider): Promise<void> {
    const childId = this.childId;

    if (!childId) {
      return;
    }

    this.removing.set(true);
    this.removeError.set(null);

    try {
      const settings = await this.aiAssistant.removeProviderApiKey(childId, provider);
      this.applySettings(settings);
      this.confirmingRemoveProvider.set(null);
    } catch {
      this.removeError.set('admin.aiProviders.remove.error');
    } finally {
      this.removing.set(false);
    }
  }

  protected async makeActive(provider: AiProvider): Promise<void> {
    const childId = this.childId;

    if (!childId) {
      return;
    }

    this.settingActiveProvider.set(provider);
    this.activeError.set(null);

    try {
      const settings = await this.aiAssistant.setActiveProvider(childId, provider);
      this.applySettings(settings);
    } catch {
      this.activeError.set('admin.aiProviders.activeError');
    } finally {
      this.settingActiveProvider.set(null);
    }
  }

  protected async testConnection(provider: AiProvider): Promise<void> {
    const childId = this.childId;

    if (!childId) {
      return;
    }

    this.testingProvider.set(provider);

    try {
      const result = await this.aiAssistant.testProviderConnection(childId, provider);
      this.setTestResult(provider, result);
    } catch {
      this.setTestResult(provider, { isSuccessful: false, errorMessage: null });
    } finally {
      this.testingProvider.set(null);
    }
  }

  private setTestResult(provider: AiProvider, result: TestProviderConnectionResult): void {
    this.testResults.update((current) => new Map(current).set(provider, result));
  }

  private clearTestResult(provider: AiProvider): void {
    this.testResults.update((current) => {
      const next = new Map(current);
      next.delete(provider);
      return next;
    });
  }

  private applySettings(settings: AiProviderSettingsData): void {
    this.configured.set(new Map(settings.providers.map((entry) => [entry.provider, entry])));
    this.activeProvider.set(settings.activeProvider);
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      const children = await this.guardians.listMyChildren();

      if (children.length === 0) {
        this.hasChildren.set(false);
        return;
      }

      this.hasChildren.set(true);
      this.childId = children[0].id;

      this.applySettings(await this.aiAssistant.listProviders(this.childId));
    } catch {
      this.error.set('admin.aiProviders.loadError');
    } finally {
      this.loading.set(false);
    }
  }
}

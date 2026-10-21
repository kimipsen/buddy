import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { GuardiansService } from '../../../../core/guardians.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import {
  MealieConnection,
  MealieConnectionView,
  MealieService,
  TestMealieConnectionResult,
  isMealieConnected,
} from '../../../../core/mealie.service';
import { createAction } from '../../../../shared/action-state/action-state';
import { Card } from '../../../../shared/card/card';

// What the card loaded: no child to connect for, or the first child's family connection.
type LoadedConnection =
  { hasChildren: false } | { hasChildren: true; childId: string; view: MealieConnectionView };

// The test's answer, or that the test request itself failed.
type TestOutcome = TestMealieConnectionResult | { kind: 'unreachable' };

// The field messages ConnectMealie's 400 carries ({ details: { baseUrl: [...], apiToken: [...] } }):
// the backend's own wording, which says what to fix (a private address, an admin token, ...).
function fieldErrors(error: unknown): Record<string, string> {
  if (error instanceof HttpErrorResponse && error.status === 400) {
    const details = (error.error as { details?: Record<string, string[]> } | null)?.details ?? {};
    return Object.fromEntries(
      Object.entries(details).map(([field, messages]) => [field, messages[0] ?? '']),
    );
  }

  return {};
}

// The family's Mealie connection (docs/backend/analysis/mealie-integration.md), next to the AI
// provider keys on the admin page. Optional: a family that never connects sees nothing else of it.
@Component({
  selector: 'app-mealie-settings',
  imports: [FormsModule, TranslatePipe, Card],
  templateUrl: './mealie-settings.html',
})
export class MealieSettings {
  private readonly guardians = inject(GuardiansService);
  private readonly mealie = inject(MealieService);

  protected readonly isConnected = isMealieConnected;

  protected readonly connection = resource({ loader: () => this.load() });

  // The form shows when not connected, or after "Replace token".
  protected readonly editing = signal(false);
  protected readonly baseUrl = signal('');
  protected readonly apiToken = signal('');
  protected readonly connecting = createAction();
  protected readonly errors = signal<Record<string, string>>({});

  protected readonly testing = signal(false);
  protected readonly testResult = signal<TestOutcome | null>(null);

  protected readonly confirmingDisconnect = signal(false);
  protected readonly disconnecting = createAction();

  // "Replace token" on a connected card: the address is prefilled, the token is asked for again.
  // Errors and the connect state are already clean here: the card only shows after a successful
  // connect or a cancel, and both clear them.
  protected startEdit(view: MealieConnection): void {
    this.editing.set(true);
    this.baseUrl.set(view.baseUrl);
    this.apiToken.set('');
    this.testResult.set(null);
  }

  protected cancelEdit(): void {
    this.editing.set(false);
    this.errors.set({});
    this.connecting.reset();
  }

  // Only reachable with both fields filled: the submit button is disabled otherwise.
  protected async connect(childId: string): Promise<void> {
    // Stryker disable next-line MethodExpression: <input type="url"> already strips surrounding whitespace from its value
    const baseUrl = this.baseUrl().trim();
    const apiToken = this.apiToken().trim();

    this.errors.set({});
    await this.connecting.run(
      true,
      async () => {
        const view = await this.mealie.connect(childId, baseUrl, apiToken);
        this.setView(view);
        this.editing.set(false);
        this.apiToken.set('');
      },
      (error) => {
        this.errors.set(fieldErrors(error));
        return 'mealie.settings.form.connectError';
      },
    );
  }

  protected async test(childId: string): Promise<void> {
    this.testing.set(true);
    this.testResult.set(null);

    try {
      this.testResult.set(await this.mealie.testConnection(childId));
    } catch {
      this.testResult.set({ kind: 'unreachable' });
    } finally {
      this.testing.set(false);
    }
  }

  protected requestDisconnect(): void {
    this.disconnecting.reset();
    this.confirmingDisconnect.set(true);
  }

  protected cancelDisconnect(): void {
    this.confirmingDisconnect.set(false);
  }

  protected async disconnect(childId: string): Promise<void> {
    await this.disconnecting.run(
      true,
      async () => {
        await this.mealie.disconnect(childId);
        this.setView({ kind: 0 });
        this.confirmingDisconnect.set(false);
        this.testResult.set(null);
      },
      'mealie.settings.disconnectError',
    );
  }

  private setView(view: MealieConnectionView): void {
    this.connection.update((current) => (current?.hasChildren ? { ...current, view } : current));
  }

  private async load(): Promise<LoadedConnection> {
    const [firstChild] = await this.guardians.listMyChildren();

    if (!firstChild) {
      return { hasChildren: false };
    }

    return {
      hasChildren: true,
      childId: firstChild.id,
      view: await this.mealie.getConnection(firstChild.id),
    };
  }
}

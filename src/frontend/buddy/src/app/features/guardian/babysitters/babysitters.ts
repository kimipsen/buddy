import { Component, computed, inject, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { Babysitter, BabysittersService } from '../../../core/babysitters.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { createAction } from '../../../shared/action-state/action-state';
import { LoadingSpinner } from '../../../shared/loading-spinner/loading-spinner';
import { Card } from '../../../shared/card/card';
import { Page } from '../../../shared/page/page';

// The guardian's own saved babysitters (see docs/backend/analysis/babysitters.md): add, edit and
// remove (archive) them. Archived ones are hidden here; they only stay on the server so pickup
// slots that still point at them keep their name. Every guardian of a child picks from these in
// the pickup planner.
@Component({
  selector: 'app-guardian-babysitters',
  imports: [FormsModule, TranslatePipe, LoadingSpinner, Card, Page],
  templateUrl: './babysitters.html',
})
export class GuardianBabysitters {
  private readonly babysitters = inject(BabysittersService);

  protected readonly list = resource({ loader: () => this.babysitters.listMine() });

  protected readonly active = computed(() =>
    (this.list.hasValue() ? this.list.value() : []).filter((b) => !b.isArchived),
  );

  protected readonly newName = signal('');
  protected readonly newContactInfo = signal('');

  protected readonly editingId = signal<string | null>(null);
  protected readonly editName = signal('');
  protected readonly editContactInfo = signal('');

  protected readonly saving = createAction();

  protected async add(): Promise<void> {
    await this.run('babysitters.saveError', async () => {
      await this.babysitters.add({
        name: this.newName().trim(),
        contactInfo: this.newContactInfo().trim(),
      });
      this.newName.set('');
      this.newContactInfo.set('');
    });
  }

  protected startEdit(babysitter: Babysitter): void {
    this.editingId.set(babysitter.id);
    this.editName.set(babysitter.name);
    this.editContactInfo.set(babysitter.contactInfo);
    this.saving.clearError();
  }

  protected cancelEdit(): void {
    this.editingId.set(null);
  }

  protected async saveEdit(babysitterId: string): Promise<void> {
    await this.run('babysitters.saveError', async () => {
      await this.babysitters.update(babysitterId, {
        name: this.editName().trim(),
        contactInfo: this.editContactInfo().trim(),
      });
      this.editingId.set(null);
    });
  }

  protected async archive(babysitterId: string): Promise<void> {
    await this.run('babysitters.archiveError', () => this.babysitters.archive(babysitterId));
  }

  private async run(errorKey: string, action: () => Promise<unknown>): Promise<void> {
    await this.saving.run(
      true,
      async () => {
        await action();
        this.list.reload();
      },
      errorKey,
    );
  }
}

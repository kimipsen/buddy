import { Component, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import {
  WorkLocation,
  WorkLocationSchedule,
  WorkLocationsService,
} from '../../../../core/work-locations.service';
import {
  ColorSwatchPicker,
  DEFAULT_COLOR_SWATCHES,
} from '../../../../shared/color-swatch-picker/color-swatch-picker';

const DEFAULT_ICON = '🏢';

// Add, edit and remove (archive) the guardian's customizable locations. Archived ones are hidden
// here; they only stay on the server so old exceptions keep resolving.
@Component({
  selector: 'app-manage-work-locations',
  imports: [FormsModule, TranslatePipe, ColorSwatchPicker],
  templateUrl: './manage-work-locations.html',
})
export class ManageWorkLocations {
  private readonly workLocations = inject(WorkLocationsService);

  readonly schedule = input.required<WorkLocationSchedule>();
  readonly changed = output<void>();

  protected readonly active = computed(() =>
    this.schedule().locations.filter((l) => !l.isArchived),
  );

  protected readonly newName = signal('');
  protected readonly newIcon = signal(DEFAULT_ICON);
  protected readonly newColor = signal<string>(DEFAULT_COLOR_SWATCHES[6]);

  protected readonly editingId = signal<string | null>(null);
  protected readonly editName = signal('');
  protected readonly editIcon = signal('');
  protected readonly editColor = signal('');

  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected async add(): Promise<void> {
    await this.run('workLocations.locations.saveError', async () => {
      await this.workLocations.addLocation({
        name: this.newName().trim(),
        icon: this.newIcon().trim(),
        color: this.newColor(),
      });
      this.newName.set('');
      this.newIcon.set(DEFAULT_ICON);
    });
  }

  protected startEdit(location: WorkLocation): void {
    this.editingId.set(location.id);
    this.editName.set(location.name);
    this.editIcon.set(location.icon);
    this.editColor.set(location.color);
    this.error.set(null);
  }

  protected cancelEdit(): void {
    this.editingId.set(null);
  }

  protected async saveEdit(locationId: string): Promise<void> {
    await this.run('workLocations.locations.saveError', async () => {
      await this.workLocations.updateLocation(locationId, {
        name: this.editName().trim(),
        icon: this.editIcon().trim(),
        color: this.editColor(),
      });
      this.editingId.set(null);
    });
  }

  protected async archive(locationId: string): Promise<void> {
    await this.run('workLocations.locations.archiveError', () =>
      this.workLocations.archiveLocation(locationId),
    );
  }

  private async run(errorKey: string, action: () => Promise<unknown>): Promise<void> {
    this.saving.set(true);
    this.error.set(null);

    try {
      await action();
      this.changed.emit();
    } catch {
      this.error.set(errorKey);
    } finally {
      this.saving.set(false);
    }
  }
}

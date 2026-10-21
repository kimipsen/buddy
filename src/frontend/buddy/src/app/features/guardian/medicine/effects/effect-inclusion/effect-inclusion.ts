import { Component, computed, inject, input, output, signal } from '@angular/core';

import { TranslatePipe } from '../../../../../core/i18n/translate.pipe';
import { regimenMedicines } from '../../../../../core/medicine-effects';
import { MedicineEffectNote, MedicinesService } from '../../../../../core/medicines.service';
import { Toggle } from '../../../../../shared/toggle/toggle';

// One switch per medicine in the summary's notes, to leave e.g. vitamin D out of the dose periods
// and the doctor visit summary (docs/backend/analysis/medicine-effect-inclusion.md). Listed from the
// notes, not the schedules, so a stopped medicine that still shapes old periods is there too.
@Component({
  selector: 'app-effect-inclusion',
  imports: [Toggle, TranslatePipe],
  templateUrl: './effect-inclusion.html',
})
export class EffectInclusion {
  private readonly medicines = inject(MedicinesService);

  readonly childId = input.required<string>();
  readonly notes = input.required<MedicineEffectNote[]>();
  // After a save, so the page reloads the notes (and with them the periods).
  readonly changed = output();

  protected readonly items = computed(() => regimenMedicines(this.notes()));
  protected readonly savingId = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);

  protected async setIncluded(medicineId: string, included: boolean): Promise<void> {
    this.savingId.set(medicineId);
    this.error.set(null);

    try {
      await this.medicines.setEffectInclusion(this.childId(), medicineId, included);
      this.changed.emit();
    } catch {
      this.error.set('medicine.effects.inclusion.saveError');
    } finally {
      this.savingId.set(null);
    }
  }
}

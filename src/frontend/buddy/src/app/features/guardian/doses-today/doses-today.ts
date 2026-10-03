import { Component, inject, resource } from '@angular/core';
import { RouterLink } from '@angular/router';

import { todayIsoDate } from '../../../core/date-utils';
import { GuardiansService } from '../../../core/guardians.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import {
  PER_ITEM_REQUEST_CONCURRENCY,
  mapWithConcurrency,
} from '../../../core/map-with-concurrency';
import {
  DoseStatus,
  MedicineDoseOccurrence,
  MedicinesService,
} from '../../../core/medicines.service';
import { createAction } from '../../../shared/action-state/action-state';
import { LoadingSpinner } from '../../../shared/loading-spinner/loading-spinner';

const PENDING: DoseStatus = 0;
const TAKEN: DoseStatus = 1;
const SKIPPED: DoseStatus = 2;

type DoseRow = MedicineDoseOccurrence & { childId: string; childName: string };

// What the widget loaded: whether the guardian has children at all, and today's doses across them.
interface LoadedDoses {
  hasChildren: boolean;
  multipleChildren: boolean;
  doses: DoseRow[];
}

@Component({
  selector: 'app-doses-today',
  imports: [RouterLink, TranslatePipe, LoadingSpinner],
  templateUrl: './doses-today.html',
})
export class DosesToday {
  private readonly guardians = inject(GuardiansService);
  private readonly medicines = inject(MedicinesService);

  protected readonly pending = PENDING;
  protected readonly taken = TAKEN;
  protected readonly skipped = SKIPPED;

  protected readonly today = resource({ loader: () => this.loadDoses() });
  protected readonly saving = createAction<string>();

  protected key(dose: DoseRow): string {
    return `${dose.childId}|${dose.medicineId}|${dose.time}`;
  }

  protected async setStatus(dose: DoseRow, status: DoseStatus): Promise<void> {
    const key = this.key(dose);

    await this.saving.run(
      key,
      async () => {
        const updated = await this.medicines.setDoseStatus(
          dose.childId,
          dose.medicineId,
          dose.date,
          dose.time,
          status,
        );
        this.today.update(
          (current) =>
            current && {
              ...current,
              doses: current.doses.map((row) =>
                this.key(row) === key ? { ...row, status: updated.status } : row,
              ),
            },
        );
      },
      'dashboard.doses.updateError',
    );
  }

  private async loadDoses(): Promise<LoadedDoses> {
    const children = await this.guardians.listMyChildren();

    if (children.length === 0) {
      return { hasChildren: false, multipleChildren: false, doses: [] };
    }

    const today = todayIsoDate();
    // One listDoses per child (no batch endpoint), bounded so a large family doesn't burst the
    // API. Still all-or-nothing: any child's failure shows the widget's load error.
    const perChild = await mapWithConcurrency(
      children,
      PER_ITEM_REQUEST_CONCURRENCY,
      async (child) => {
        const occurrences = await this.medicines.listDoses(child.id, today, today);
        return occurrences.map((occurrence) => ({
          ...occurrence,
          childId: child.id,
          childName: child.name.givenName,
        }));
      },
    );

    return {
      hasChildren: true,
      multipleChildren: children.length > 1,
      doses: perChild.flat().sort((a, b) => a.time.localeCompare(b.time)),
    };
  }
}

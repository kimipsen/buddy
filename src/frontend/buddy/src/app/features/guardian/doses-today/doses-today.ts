import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { todayIsoDate } from '../../../core/date-utils';
import { GuardiansService } from '../../../core/guardians.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { DoseStatus, MedicineDoseOccurrence, MedicinesService } from '../../../core/medicines.service';
import { LoadingSpinner } from '../../../shared/loading-spinner/loading-spinner';

const PENDING: DoseStatus = 0;
const TAKEN: DoseStatus = 1;
const SKIPPED: DoseStatus = 2;

type DoseRow = MedicineDoseOccurrence & { childId: string; childName: string };

@Component({
  selector: 'app-doses-today',
  imports: [RouterLink, TranslatePipe, LoadingSpinner],
  templateUrl: './doses-today.html'
})
export class DosesToday implements OnInit {
  private readonly guardians = inject(GuardiansService);
  private readonly medicines = inject(MedicinesService);

  protected readonly pending = PENDING;
  protected readonly taken = TAKEN;
  protected readonly skipped = SKIPPED;

  protected readonly doses = signal<DoseRow[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly hasChildren = signal(true);
  protected readonly multipleChildren = signal(false);
  protected readonly savingKey = signal<string | null>(null);

  ngOnInit(): void {
    void this.loadDoses();
  }

  protected key(dose: DoseRow): string {
    return `${dose.childId}|${dose.medicineId}|${dose.time}`;
  }

  protected async setStatus(dose: DoseRow, status: DoseStatus): Promise<void> {
    const key = this.key(dose);
    this.savingKey.set(key);
    this.error.set(null);

    try {
      const updated = await this.medicines.setDoseStatus(dose.childId, dose.medicineId, dose.date, dose.time, status);
      this.doses.update((current) =>
        current.map((row) => (this.key(row) === key ? { ...row, status: updated.status } : row))
      );
    } catch {
      this.error.set('dashboard.doses.updateError');
    } finally {
      this.savingKey.set(null);
    }
  }

  private async loadDoses(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      const children = await this.guardians.listMyChildren();

      if (children.length === 0) {
        this.hasChildren.set(false);
        return;
      }

      this.hasChildren.set(true);
      this.multipleChildren.set(children.length > 1);

      const today = todayIsoDate();
      const perChild = await Promise.all(
        children.map(async (child) => {
          const occurrences = await this.medicines.listDoses(child.id, today, today);
          return occurrences.map((occurrence) => ({
            ...occurrence,
            childId: child.id,
            childName: child.name.givenName
          }));
        })
      );

      this.doses.set(perChild.flat().sort((a, b) => a.time.localeCompare(b.time)));
    } catch {
      this.error.set('dashboard.doses.loadError');
    } finally {
      this.loading.set(false);
    }
  }
}

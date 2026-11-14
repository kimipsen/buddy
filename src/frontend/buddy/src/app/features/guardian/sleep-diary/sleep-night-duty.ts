import { inject, Injectable } from '@angular/core';

import { addDaysIso } from '../../../core/date-utils';
import { FeaturesService } from '../../../core/features.service';
import { GuardianSummary, GuardiansService } from '../../../core/guardians.service';
import { TranslationService } from '../../../core/i18n/translation.service';
import { Night, NightDutyService, nightEntry } from '../../../core/night-duty.service';
import { responsibleText, ResponsibleText } from '../night-duty/night-people';

// One ListNights read may span to - from <= NightDutyRules.MaxRangeDays (31). Reading 31 nights at
// a time (to - from = 30) stays inside that limit, and 92 nights take three reads.
export const NIGHTS_PER_READ = 31;

// Who was on duty a night (night duty, GitHub issue #68): the person, or null for a note on its own.
export interface NightDutyCell {
  who: ResponsibleText | null;
  note: string;
}

export interface NightDutyData {
  nights: Night[];
  guardians: GuardianSummary[];
}

// Consecutive windows of at most NIGHTS_PER_READ nights that cover from..to, both included. Steps
// with addDaysIso (calendar days), never with getTime() differences, so DST can't shift a window.
export function nightDutyWindows(from: string, to: string): { from: string; to: string }[] {
  const windows: { from: string; to: string }[] = [];
  let start = from;

  while (start <= to) {
    const last = addDaysIso(start, NIGHTS_PER_READ - 1);
    const end = last < to ? last : to;
    windows.push({ from: start, to: end });
    start = addDaysIso(end, 1);
  }

  return windows;
}

// Night duty for any period, read from night duty and never copied into the diary.
@Injectable({ providedIn: 'root' })
export class SleepNightDuty {
  private readonly features = inject(FeaturesService);
  private readonly nightDuty = inject(NightDutyService);
  private readonly guardians = inject(GuardiansService);
  private readonly translation = inject(TranslationService);

  // null hides the column: the nightDuty feature is off, or a window failed to load. A column with
  // holes would read as "nobody was on duty", so one failed window hides the whole column.
  async load(childId: string, from: string, to: string): Promise<NightDutyData | null> {
    if (!this.features.enabled('nightDuty')) {
      return null;
    }

    try {
      const [windows, guardians] = await Promise.all([
        Promise.all(
          nightDutyWindows(from, to).map((w) => this.nightDuty.listNights(childId, w.from, w.to)),
        ),
        // Without names a guardian's night still shows "A guardian".
        this.guardians.listChildGuardians(childId).catch((): GuardianSummary[] => []),
      ]);
      return { nights: windows.flat(), guardians };
    } catch {
      return null;
    }
  }

  // Planned nights only, named with responsibleText and the nightDuty.who.* labels.
  cells(data: NightDutyData): Partial<Record<string, NightDutyCell>> {
    const people = { guardians: data.guardians, babysitters: [] };
    const labels = {
      guardian: this.translation.translate('nightDuty.who.guardian'),
      babysitter: this.translation.translate('nightDuty.who.babysitter'),
      sleepover: (host: string) => this.translation.translate('nightDuty.who.sleepover', { host }),
    };
    const byDate: Partial<Record<string, NightDutyCell>> = {};

    for (const night of data.nights) {
      const entry = nightEntry(night);

      if (entry) {
        byDate[night.date] = {
          who: responsibleText(entry.responsible, people, labels),
          note: entry.note ?? '',
        };
      }
    }

    return byDate;
  }
}

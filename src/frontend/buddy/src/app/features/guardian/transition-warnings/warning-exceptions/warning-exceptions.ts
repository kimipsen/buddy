import { Component, computed, inject, input, output, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { CalendarsService } from '../../../../core/calendars.service';
import { addDaysIso, todayIsoDate } from '../../../../core/date-utils';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { errorCode } from '../../../../core/progress.service';
import {
  ChildTransitionWarnings,
  ItemWarningOverrideDetail,
  TOO_MANY_WARNING_OVERRIDES,
  TransitionWarningsService,
} from '../../../../core/transition-warnings.service';
import { Toggle } from '../../../../shared/toggle/toggle';
import { LeadChips } from '../lead-chips/lead-chips';

// How far ahead "Add exception" looks for the child's items.
export const EXCEPTION_LOOKAHEAD_DAYS = 14;

export interface ExceptionCandidate {
  itemId: string;
  title: string;
  icon: string;
}

/**
 * A child's per-item exceptions: their own lead times, or none ("No heads-up"). New ones are picked
 * from the events and the child's own tasks in the next 14 days (Frontend section of the design).
 */
@Component({
  selector: 'app-warning-exceptions',
  imports: [FormsModule, TranslatePipe, Toggle, LeadChips],
  templateUrl: './warning-exceptions.html',
})
export class WarningExceptions {
  private readonly api = inject(TransitionWarningsService);
  private readonly calendars = inject(CalendarsService);
  private readonly translation = inject(TranslationService);

  readonly childId = input.required<string>();
  readonly childName = input('');
  readonly overrides = input<readonly ItemWarningOverrideDetail[]>([]);
  readonly saved = output<ChildTransitionWarnings>();
  readonly removed = output<string>();

  protected readonly adding = signal(false);
  protected readonly itemId = signal('');
  protected readonly never = signal(false);
  protected readonly leadMinutes = signal<number[]>([10]);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  // Only asked for once "Add exception" opens.
  private readonly upcoming = resource({
    params: () => (this.adding() ? { childId: this.childId() } : undefined),
    loader: ({ params }) => this.loadCandidates(params.childId),
  });
  protected readonly loadingItems = computed(() => this.upcoming.isLoading());
  protected readonly candidates = computed(() => {
    const taken = new Set(this.overrides().map((o) => o.itemId));
    const items = this.upcoming.hasValue() ? this.upcoming.value() : [];
    return items.filter((item) => !taken.has(item.itemId));
  });
  protected readonly canSave = computed(
    () => this.itemId() !== '' && (this.never() || this.leadMinutes().length > 0) && !this.busy(),
  );

  protected leadText(override: ItemWarningOverrideDetail): string {
    if (override.leadMinutes.length === 0) {
      return this.translation.translate('transitionWarnings.exceptions.never');
    }
    return new Intl.ListFormat(this.translation.language(), { type: 'unit' }).format(
      override.leadMinutes.map((minutes) =>
        this.translation.translate('transitionWarnings.leadTimes.minutes', { minutes }),
      ),
    );
  }

  protected startAdding(): void {
    this.adding.set(true);
    this.itemId.set('');
    this.never.set(false);
    this.leadMinutes.set([10]);
    this.error.set(null);
  }

  protected cancel(): void {
    this.adding.set(false);
    this.error.set(null);
  }

  protected async save(): Promise<void> {
    if (!this.canSave()) {
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    try {
      const settings = await this.api.setOverride(
        this.childId(),
        this.itemId(),
        this.never() ? [] : this.leadMinutes(),
      );
      this.adding.set(false);
      this.saved.emit(settings);
    } catch (error) {
      this.error.set(
        errorCode(error) === TOO_MANY_WARNING_OVERRIDES
          ? 'transitionWarnings.exceptions.tooMany'
          : 'transitionWarnings.exceptions.error',
      );
    } finally {
      this.busy.set(false);
    }
  }

  protected async remove(override: ItemWarningOverrideDetail): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      await this.api.clearOverride(this.childId(), override.itemId);
      this.removed.emit(override.itemId);
    } catch {
      this.error.set('transitionWarnings.exceptions.error');
    } finally {
      this.busy.set(false);
    }
  }

  // Events, and tasks assigned to this child, once per item (a routine by its own title).
  private async loadCandidates(childId: string): Promise<ExceptionCandidate[]> {
    const today = todayIsoDate();
    const occurrences = await this.calendars.listOccurrencesInRange(
      today,
      addDaysIso(today, EXCEPTION_LOOKAHEAD_DAYS - 1),
    );
    const byId = new Map<string, ExceptionCandidate>();

    for (const occurrence of occurrences) {
      if (occurrence.kind !== 'Event' && occurrence.assignedTo !== childId) {
        continue;
      }
      if (!byId.has(occurrence.itemId)) {
        byId.set(occurrence.itemId, {
          itemId: occurrence.itemId,
          title: occurrence.routine?.parentTitle ?? occurrence.title,
          icon: occurrence.routine?.parentIcon ?? occurrence.icon,
        });
      }
    }

    return [...byId.values()].sort((a, b) => a.title.localeCompare(b.title));
  }
}

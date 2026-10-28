import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, input, output, signal } from '@angular/core';

import { DayOfWeek } from '../../../../core/date-utils';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TaskLibraryService, TaskTemplate } from '../../../../core/task-library.service';
import { createAction } from '../../../../shared/action-state/action-state';
import { WeekdayPicker } from '../../../../shared/weekday-picker/weekday-picker';

// How long "Added to your list" / "Not added this time" stays under the list after a guardian
// answered. A client-side window on resolvedAt: there is no "dismiss" on the server
// (docs/backend/analysis/packing-checklists.md, Question 8).
export const RESOLVED_SUGGESTION_DAYS = 3;
const DAY_MS = 24 * 60 * 60 * 1000;

// The two conflicts a child can run into, shown in their own words rather than as an error.
const CONFLICT_MESSAGES: Record<string, string> = {
  checklist_item_exists: 'child.home.packing.alreadyOnList',
  too_many_pending_suggestions: 'child.home.packing.tooMany',
};

function suggestErrorMessage(error: unknown): string {
  const code =
    error instanceof HttpErrorResponse
      ? (error.error as { code?: string } | null)?.code
      : undefined;
  return (code && CONFLICT_MESSAGES[code]) ?? 'child.home.packing.error';
}

// Under a packing list on the child home: "Suggest something" (a title and, optionally, the days
// it's needed), the child's ideas waiting for a grown-up (each can be taken back), and for a few
// days what a grown-up decided. Emits the updated template so the home can keep its copy.
@Component({
  selector: 'app-child-suggestions',
  imports: [TranslatePipe, WeekdayPicker],
  templateUrl: './child-suggestions.html',
})
export class ChildSuggestions {
  private readonly taskLibrary = inject(TaskLibraryService);

  readonly template = input.required<TaskTemplate>();
  // The home's ticking clock, so a resolved line drops out after three days without a reload.
  readonly now = input(Date.now());
  readonly changed = output<TaskTemplate>();

  protected readonly pending = computed(() =>
    this.template().suggestions.filter((suggestion) => suggestion.status === 'Pending'),
  );
  protected readonly recentlyResolved = computed(() =>
    this.template().suggestions.filter(
      (suggestion) =>
        (suggestion.status === 'Approved' || suggestion.status === 'Declined') &&
        suggestion.resolvedAt !== null &&
        this.now() - new Date(suggestion.resolvedAt).getTime() < RESOLVED_SUGGESTION_DAYS * DAY_MS,
    ),
  );

  protected readonly open = signal(false);
  // Stryker disable next-line StringLiteral: the form that reads title() only opens through openSheet(), which resets it to ''
  protected readonly title = signal('');
  protected readonly weekdays = signal<DayOfWeek[]>([]);
  protected readonly saving = createAction<string>();

  protected openSheet(): void {
    this.open.set(true);
    this.title.set('');
    this.weekdays.set([]);
    this.saving.clearError();
  }

  protected closeSheet(): void {
    this.open.set(false);
  }

  protected async send(): Promise<void> {
    const title = this.title().trim();

    if (!title) {
      return;
    }

    await this.saving.run(
      'suggest',
      async () => {
        const updated = await this.taskLibrary.suggestChecklistItem(
          this.template().id,
          title,
          this.weekdays(),
        );
        this.open.set(false);
        this.changed.emit(updated);
      },
      suggestErrorMessage,
    );
  }

  protected async withdraw(suggestionId: string): Promise<void> {
    await this.saving.run(
      suggestionId,
      async () => {
        this.changed.emit(
          await this.taskLibrary.withdrawChecklistSuggestion(this.template().id, suggestionId),
        );
      },
      'child.home.packing.error',
    );
  }
}

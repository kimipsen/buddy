import { Component, computed, inject, input, signal } from '@angular/core';

import { DayOfWeek } from '../../../../../core/date-utils';
import { TranslatePipe } from '../../../../../core/i18n/translate.pipe';
import {
  ChecklistSuggestion,
  TaskLibraryService,
  TaskTemplate,
} from '../../../../../core/task-library.service';
import { createAction } from '../../../../../shared/action-state/action-state';
import {
  WeekdayPicker,
  weekdayShortKey,
} from '../../../../../shared/weekday-picker/weekday-picker';

// The child's pending suggestions for one packing list (docs/backend/analysis/packing-checklists.md,
// Question 8). Approve opens an inline form pre-filled with the child's title and days, so the
// guardian confirms or adjusts them; "Not now" declines. The service replaces the template in its
// shared state, so the list above redraws with the new item.
@Component({
  selector: 'app-suggestion-panel',
  imports: [TranslatePipe, WeekdayPicker],
  templateUrl: './suggestion-panel.html',
})
export class SuggestionPanel {
  private readonly taskLibrary = inject(TaskLibraryService);

  readonly template = input.required<TaskTemplate>();
  readonly childName = input('');

  protected readonly pending = computed(() =>
    this.template().suggestions.filter((suggestion) => suggestion.status === 'Pending'),
  );

  protected readonly approvingId = signal<string | null>(null);
  protected readonly approveTitle = signal('');
  protected readonly approveWeekdays = signal<DayOfWeek[]>([]);
  protected readonly resolving = createAction<string>();

  protected readonly shortKey = weekdayShortKey;

  protected startApprove(suggestion: ChecklistSuggestion): void {
    this.approvingId.set(suggestion.id);
    this.approveTitle.set(suggestion.title);
    this.approveWeekdays.set([...suggestion.weekdays]);
    this.resolving.clearError();
  }

  protected cancelApprove(): void {
    this.approvingId.set(null);
  }

  protected async approve(suggestion: ChecklistSuggestion): Promise<void> {
    const title = this.approveTitle().trim();

    if (!title) {
      return;
    }

    await this.resolving.run(
      suggestion.id,
      async () => {
        await this.taskLibrary.approveChecklistSuggestion(
          this.template().id,
          suggestion.id,
          title,
          this.approveWeekdays(),
        );
        this.approvingId.set(null);
      },
      'taskLibrary.manageTasks.suggestions.error',
    );
  }

  protected async decline(suggestion: ChecklistSuggestion): Promise<void> {
    await this.resolving.run(
      suggestion.id,
      async () => {
        await this.taskLibrary.declineChecklistSuggestion(this.template().id, suggestion.id);
      },
      'taskLibrary.manageTasks.suggestions.error',
    );
  }
}

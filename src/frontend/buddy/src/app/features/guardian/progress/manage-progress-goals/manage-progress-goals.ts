import { Component, computed, inject, linkedSignal, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { GuardiansService } from '../../../../core/guardians.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { GoalPost, ProgressService, ProgressSummary } from '../../../../core/progress.service';
import { createAction } from '../../../../shared/action-state/action-state';
import { Card } from '../../../../shared/card/card';

interface GoalPostRow {
  threshold: string;
  icon: string;
  label: string;
}

function toRow(goalPost: GoalPost): GoalPostRow {
  return {
    threshold: String(goalPost.threshold),
    icon: goalPost.icon,
    label: goalPost.label,
  };
}

@Component({
  selector: 'app-manage-progress-goals',
  imports: [FormsModule, TranslatePipe, Card],
  templateUrl: './manage-progress-goals.html',
})
export class ManageProgressGoals {
  private readonly guardians = inject(GuardiansService);
  private readonly progressService = inject(ProgressService);

  protected readonly children = resource({ loader: () => this.guardians.listMyChildren() });
  protected readonly childList = computed(() =>
    this.children.hasValue() ? this.children.value() : [],
  );
  protected readonly selectedChildId = linkedSignal(() => this.childList()[0]?.id);

  // The selected child's progress; idle until a child is selected.
  protected readonly progress = resource({
    params: () => {
      const childId = this.selectedChildId();
      return childId ? { childId } : undefined;
    },
    loader: ({ params }) => this.progressService.getChildProgress(params.childId),
  });

  // The editable draft, reset from the loaded (or just saved) goal posts.
  protected readonly rows = linkedSignal(() =>
    this.progress.hasValue() ? this.progress.value().goalPosts.map(toRow) : [],
  );

  protected readonly loading = computed(
    () => this.children.isLoading() || this.progress.isLoading(),
  );
  protected readonly hasChildren = computed(
    () => !this.children.hasValue() || this.children.value().length > 0,
  );
  protected readonly loadFailed = computed(
    () => !!this.children.error() || !!this.progress.error(),
  );

  protected readonly saving = createAction();
  protected readonly saved = signal(false);

  protected onChildChange(childId: string): void {
    this.selectedChildId.set(childId);
    this.saved.set(false);
    this.saving.clearError();
  }

  protected addRow(): void {
    this.rows.update((rows) => [...rows, { threshold: '', icon: '🌱', label: '' }]);
    this.saved.set(false);
  }

  protected removeRow(index: number): void {
    this.rows.update((rows) => rows.filter((_, i) => i !== index));
    this.saved.set(false);
  }

  protected setThreshold(index: number, value: string): void {
    this.rows.update((rows) =>
      rows.map((row, i) => (i === index ? { ...row, threshold: value } : row)),
    );
    this.saved.set(false);
  }

  protected setIcon(index: number, value: string): void {
    this.rows.update((rows) => rows.map((row, i) => (i === index ? { ...row, icon: value } : row)));
    this.saved.set(false);
  }

  protected setLabel(index: number, value: string): void {
    this.rows.update((rows) =>
      rows.map((row, i) => (i === index ? { ...row, label: value } : row)),
    );
    this.saved.set(false);
  }

  protected canSave(): boolean {
    return (
      this.rows().length > 0 &&
      this.rows().every((row) => Number(row.threshold) > 0 && row.icon.trim().length > 0)
    );
  }

  protected async save(): Promise<void> {
    const childId = this.selectedChildId();

    if (!childId || !this.canSave()) {
      return;
    }

    this.saved.set(false);

    await this.saving.run(
      true,
      async () => {
        const goalPosts: GoalPost[] = this.rows().map((row) => ({
          threshold: Number(row.threshold),
          icon: row.icon.trim(),
          label: row.label.trim(),
        }));

        let summary: ProgressSummary;
        try {
          summary = await this.progressService.configureGoalPosts(childId, goalPosts);
        } catch (error: unknown) {
          // Switched child while saving: the failed draft is no longer on screen, so its error
          // would only be misread as being about the new child.
          if (this.selectedChildId() !== childId) {
            return;
          }
          throw error;
        }
        // Switched child while saving: the response is the previous child's goals, and setting it
        // would cancel or overwrite the new child's load (so the next Save would send them there).
        if (this.selectedChildId() !== childId || this.progress.isLoading()) {
          return;
        }
        this.progress.set(summary);
        this.saved.set(true);
      },
      'progress.manageProgressGoals.saveError',
    );
  }
}

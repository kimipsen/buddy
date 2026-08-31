import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { ChildSummary, GuardiansService } from '../../../../core/guardians.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { GoalPost, ProgressService } from '../../../../core/progress.service';

interface GoalPostRow {
  threshold: string;
  icon: string;
  label: string;
}

function toRow(goalPost: GoalPost): GoalPostRow {
  return { threshold: String(goalPost.threshold), icon: goalPost.icon, label: goalPost.label ?? '' };
}

@Component({
  selector: 'app-manage-progress-goals',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './manage-progress-goals.html'
})
export class ManageProgressGoals implements OnInit {
  private readonly guardians = inject(GuardiansService);
  private readonly progressService = inject(ProgressService);

  protected readonly hasChildren = signal(true);
  protected readonly children = signal<ChildSummary[]>([]);
  protected readonly selectedChildId = signal<string | null>(null);

  protected readonly rows = signal<GoalPostRow[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly saving = signal(false);
  protected readonly saveError = signal<string | null>(null);
  protected readonly saved = signal(false);

  ngOnInit(): void {
    void this.loadChildren();
  }

  protected async onChildChange(childId: string): Promise<void> {
    this.selectedChildId.set(childId);
    await this.loadGoalPosts(childId);
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
    this.rows.update((rows) => rows.map((row, i) => (i === index ? { ...row, threshold: value } : row)));
    this.saved.set(false);
  }

  protected setIcon(index: number, value: string): void {
    this.rows.update((rows) => rows.map((row, i) => (i === index ? { ...row, icon: value } : row)));
    this.saved.set(false);
  }

  protected setLabel(index: number, value: string): void {
    this.rows.update((rows) => rows.map((row, i) => (i === index ? { ...row, label: value } : row)));
    this.saved.set(false);
  }

  protected canSave(): boolean {
    return this.rows().length > 0 && this.rows().every((row) => Number(row.threshold) > 0 && row.icon.trim().length > 0);
  }

  protected async save(): Promise<void> {
    const childId = this.selectedChildId();

    if (!childId || !this.canSave()) {
      return;
    }

    this.saving.set(true);
    this.saveError.set(null);
    this.saved.set(false);

    try {
      const goalPosts: GoalPost[] = this.rows().map((row) => ({
        threshold: Number(row.threshold),
        icon: row.icon.trim(),
        label: row.label.trim() || null
      }));

      const summary = await this.progressService.configureGoalPosts(childId, goalPosts);
      this.rows.set(summary.goalPosts.map(toRow));
      this.saved.set(true);
    } catch {
      this.saveError.set('progress.manageProgressGoals.saveError');
    } finally {
      this.saving.set(false);
    }
  }

  private async loadChildren(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      const children = await this.guardians.listMyChildren();

      if (children.length === 0) {
        this.hasChildren.set(false);
        return;
      }

      this.hasChildren.set(true);
      this.children.set(children);
      this.selectedChildId.set(children[0].id);
      await this.loadGoalPosts(children[0].id);
    } catch {
      this.error.set('progress.manageProgressGoals.loadError');
    } finally {
      this.loading.set(false);
    }
  }

  private async loadGoalPosts(childId: string): Promise<void> {
    this.saved.set(false);
    this.saveError.set(null);

    const summary = await this.progressService.getChildProgress(childId);
    this.rows.set(summary.goalPosts.map(toRow));
  }
}

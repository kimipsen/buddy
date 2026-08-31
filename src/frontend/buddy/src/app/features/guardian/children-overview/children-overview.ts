import { Component, OnInit, inject, signal } from '@angular/core';

import { ChildSummary, GuardiansService } from '../../../core/guardians.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { ProgressService } from '../../../core/progress.service';
import { LoadingSpinner } from '../../../shared/loading-spinner/loading-spinner';

@Component({
  selector: 'app-children-overview',
  imports: [TranslatePipe, LoadingSpinner],
  templateUrl: './children-overview.html'
})
export class ChildrenOverview implements OnInit {
  private readonly guardians = inject(GuardiansService);
  private readonly progressService = inject(ProgressService);

  protected readonly children = signal<ChildSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  // Keyed by child ID rather than joined onto ChildSummary -- progress can fail or load slower
  // per child without blocking the (more important) name/linked-status list from rendering.
  protected readonly progressByChildId = signal<Record<string, { totalStars: number; icon: string }>>({});

  ngOnInit(): void {
    void this.loadChildren();
  }

  private async loadChildren(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      const children = await this.guardians.listMyChildren();
      this.children.set(children);
      void this.loadProgress(children);
    } catch {
      this.error.set('dashboard.children.loadError');
    } finally {
      this.loading.set(false);
    }
  }

  // Best-effort, like tasks-today's assignee-name lookup: a guardian without an active link to a
  // given child (shouldn't happen here, since listMyChildren already scopes to linked children)
  // just shows no star count for that row rather than an error for the whole widget.
  private async loadProgress(children: ChildSummary[]): Promise<void> {
    const entries = await Promise.all(
      children.map(async (child) => {
        try {
          const summary = await this.progressService.getChildProgress(child.id);

          return [child.id, { totalStars: summary.totalStars, icon: summary.currentIcon ?? summary.nextGoalIcon }] as const;
        } catch {
          return null;
        }
      })
    );

    this.progressByChildId.set(Object.fromEntries(entries.filter((entry) => entry !== null)));
  }
}

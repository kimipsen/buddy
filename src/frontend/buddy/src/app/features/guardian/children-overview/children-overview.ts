import { Component, inject, resource, signal } from '@angular/core';

import { FeaturesService } from '../../../core/features.service';
import { ChildSummary, GuardiansService } from '../../../core/guardians.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { mapWithConcurrency } from '../../../core/map-with-concurrency';
import { ProgressService } from '../../../core/progress.service';
import { LoadingSpinner } from '../../../shared/loading-spinner/loading-spinner';

// How many /progress/children/{id} requests this widget keeps in flight at once. There is no
// batch progress endpoint, so it still costs one request per child, but a guardian with dozens of
// children no longer fires them all in the same instant (that burst exhausted the API's Postgres
// connection pools and 500'd the whole dashboard). See core/map-with-concurrency.ts.
export const PROGRESS_REQUEST_CONCURRENCY = 4;

@Component({
  selector: 'app-children-overview',
  imports: [TranslatePipe, LoadingSpinner],
  templateUrl: './children-overview.html',
})
export class ChildrenOverview {
  private readonly features = inject(FeaturesService);

  private readonly guardians = inject(GuardiansService);
  private readonly progressService = inject(ProgressService);

  protected readonly children = resource({ loader: () => this.loadChildren() });

  // Keyed by child ID rather than joined onto ChildSummary -- progress can fail or load slower
  // per child without blocking the (more important) name/linked-status list from rendering.
  protected readonly progressByChildId = signal<
    Record<string, { totalStars: number; icon: string }>
  >({});

  private async loadChildren(): Promise<ChildSummary[]> {
    const children = await this.guardians.listMyChildren();

    if (this.features.enabled('progress')) {
      void this.loadProgress(children);
    }

    return children;
  }

  // Best-effort, like tasks-today's assignee-name lookup: a guardian without an active link to a
  // given child (shouldn't happen here, since listMyChildren already scopes to linked children)
  // just shows no star count for that row rather than an error for the whole widget. Each badge
  // appears as soon as its own request finishes instead of waiting for the slowest child.
  private async loadProgress(children: ChildSummary[]): Promise<void> {
    await mapWithConcurrency(children, PROGRESS_REQUEST_CONCURRENCY, async (child) => {
      try {
        const summary = await this.progressService.getChildProgress(child.id);
        const entry = {
          totalStars: summary.totalStars,
          icon: summary.displayIcon,
        };

        this.progressByChildId.update((current) => ({ ...current, [child.id]: entry }));
      } catch {
        // No badge for this child; the rest still load.
      }
    });
  }
}

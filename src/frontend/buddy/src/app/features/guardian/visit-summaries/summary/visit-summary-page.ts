import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, resource, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { VisitSummariesService } from '../../../../core/visit-summaries.service';
import { createAction } from '../../../../shared/action-state/action-state';
import { LoadingSpinner } from '../../../../shared/loading-spinner/loading-spinner';
import { Page } from '../../../../shared/page/page';
import { VisitSummaryReport } from '../../../../shared/visit-summary-report/visit-summary-report';
import { VisitSummaryEditor } from '../editor/visit-summary-editor';
import { VisitSummaryShareLinks } from '../share-links/visit-summary-share-links';

// One prepared visit: the report exactly as the doctor sees it, with editing, the doctor's links,
// printing (the chromeless print route) and deleting.
@Component({
  selector: 'app-visit-summary-page',
  imports: [
    LoadingSpinner,
    Page,
    RouterLink,
    TranslatePipe,
    VisitSummaryEditor,
    VisitSummaryReport,
    VisitSummaryShareLinks,
  ],
  templateUrl: './visit-summary-page.html',
})
export class VisitSummaryPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly visitSummaries = inject(VisitSummariesService);

  protected readonly childId = this.route.snapshot.paramMap.get('childId') ?? '';
  protected readonly summaryId = this.route.snapshot.paramMap.get('summaryId') ?? '';

  protected readonly loaded = resource({
    loader: () => this.visitSummaries.get(this.childId, this.summaryId),
  });

  protected readonly notFound = computed(() => {
    const error = this.loaded.error();
    return error instanceof HttpErrorResponse && error.status === 404;
  });

  protected readonly editing = signal(false);
  protected readonly confirmingDelete = signal(false);
  protected readonly deleting = createAction();

  protected reload(): void {
    this.loaded.reload();
  }

  protected onSaved(): void {
    this.editing.set(false);
    this.loaded.reload();
  }

  protected async delete(): Promise<void> {
    await this.deleting.run(
      true,
      async () => {
        await this.visitSummaries.delete(this.childId, this.summaryId);
        await this.router.navigate(['/guardian/visit-summaries']);
      },
      'visitSummaries.page.deleteError',
    );
  }
}

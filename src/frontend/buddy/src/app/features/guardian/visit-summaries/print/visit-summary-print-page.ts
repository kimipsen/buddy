import { Component, DOCUMENT, inject, resource } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { injectPrintPageStyle } from '../../../../core/print-page-style';
import { VisitSummariesService } from '../../../../core/visit-summaries.service';
import { LoadingSpinner } from '../../../../shared/loading-spinner/loading-spinner';
import { VisitSummaryReport } from '../../../../shared/visit-summary-report/visit-summary-report';

// The A4 portrait printout of a visit summary, outside GuardianShell like the house rules
// printouts, so no navigation ends up on paper (doctor-visit-summary.md, Question 8).
@Component({
  selector: 'app-visit-summary-print-page',
  imports: [LoadingSpinner, RouterLink, TranslatePipe, VisitSummaryReport],
  templateUrl: './visit-summary-print-page.html',
})
export class VisitSummaryPrintPage {
  private readonly route = inject(ActivatedRoute);
  private readonly document = inject(DOCUMENT);
  private readonly visitSummaries = inject(VisitSummariesService);
  private readonly pageStyle = injectPrintPageStyle('visit-summary-print');

  protected readonly childId = this.route.snapshot.paramMap.get('childId') ?? '';
  protected readonly summaryId = this.route.snapshot.paramMap.get('summaryId') ?? '';

  protected readonly loaded = resource({
    loader: async ({ abortSignal }) => {
      const loaded = await this.visitSummaries.get(this.childId, this.summaryId);
      // Left before it arrived: a style added now would outlive the page.
      if (!abortSignal.aborted) {
        this.pageStyle.apply({ size: 'A4', orientation: 'portrait', margin: '12mm' });
      }
      return loaded;
    },
  });

  protected print(): void {
    this.document.defaultView?.print();
  }
}

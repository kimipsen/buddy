import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DOCUMENT, computed, inject, resource } from '@angular/core';
import { ActivatedRoute } from '@angular/router';

import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { injectPrintPageStyle } from '../../core/print-page-style';
import { VisitSummariesService } from '../../core/visit-summaries.service';
import { VisitSummaryReport } from '../../shared/visit-summary-report/visit-summary-report';

// The page a doctor opens from a visit summary link: no login and no app chrome, the report for the
// period the guardian fixed, printable on A4. The token in the URL is the only credential
// (GetSharedVisitSummary on the backend). It renders in the reader's own browser language.
@Component({
  selector: 'app-shared-visit-summary',
  imports: [DatePipe, TranslatePipe, VisitSummaryReport],
  templateUrl: './shared-visit-summary.html',
})
export class SharedVisitSummary {
  private readonly route = inject(ActivatedRoute);
  private readonly document = inject(DOCUMENT);
  private readonly visitSummaries = inject(VisitSummariesService);
  private readonly pageStyle = injectPrintPageStyle('visit-summary-print');

  private readonly token = this.route.snapshot.paramMap.get('token') ?? '';

  protected readonly shared = resource({
    loader: async ({ abortSignal }) => {
      const shared = await this.visitSummaries.getShared(this.token);
      if (!abortSignal.aborted) {
        this.pageStyle.apply({ size: 'A4', orientation: 'portrait', margin: '12mm' });
      }
      return shared;
    },
  });

  protected readonly notFound = computed(() => {
    const error = this.shared.error();
    return error instanceof HttpErrorResponse && error.status === 404;
  });

  protected print(): void {
    this.document.defaultView?.print();
  }
}

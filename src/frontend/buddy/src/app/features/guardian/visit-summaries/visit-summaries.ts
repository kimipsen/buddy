import { Component, computed, inject, linkedSignal, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { parseIsoDate } from '../../../core/date-utils';
import { ChildSummary, GuardiansService } from '../../../core/guardians.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';
import { VisitSummariesService, VisitSummary } from '../../../core/visit-summaries.service';
import { Page } from '../../../shared/page/page';
import { VisitSummaryEditor } from './editor/visit-summary-editor';

// The guardian's prepared doctor's visits, per child, newest visit first
// (docs/backend/analysis/doctor-visit-summary.md). A new summary opens straight on its report.
@Component({
  selector: 'app-guardian-visit-summaries',
  imports: [FormsModule, Page, RouterLink, TranslatePipe, VisitSummaryEditor],
  templateUrl: './visit-summaries.html',
})
export class GuardianVisitSummaries {
  private readonly guardians = inject(GuardiansService);
  private readonly visitSummaries = inject(VisitSummariesService);
  private readonly translation = inject(TranslationService);
  private readonly router = inject(Router);

  protected readonly children = resource({ loader: () => this.guardians.listMyChildren() });
  protected readonly childList = computed((): ChildSummary[] =>
    this.children.hasValue() ? this.children.value() : [],
  );
  private readonly requestedChildId =
    inject(ActivatedRoute, { optional: true })?.snapshot.queryParamMap.get('child') ?? undefined;
  protected readonly selectedChildId = linkedSignal(
    () =>
      this.childList().find((child) => child.id === this.requestedChildId)?.id ??
      this.childList()[0]?.id,
  );

  protected readonly summaries = resource({
    params: () => this.selectedChildId(),
    loader: ({ params: childId }) => this.visitSummaries.list(childId),
  });

  protected readonly creating = signal(false);

  protected selectChild(childId: string): void {
    this.selectedChildId.set(childId);
    this.creating.set(false);
  }

  protected opened(summary: VisitSummary): void {
    void this.router.navigate(['/guardian/visit-summaries', summary.childId, summary.id]);
  }

  protected formatDate(date: string): string {
    return parseIsoDate(date).toLocaleDateString(this.translation.language(), {
      day: 'numeric',
      month: 'short',
      year: 'numeric',
    });
  }
}

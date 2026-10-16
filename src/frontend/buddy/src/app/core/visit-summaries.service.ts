import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import type { Schemas } from './api/schemas';
import { postIdempotent } from './http-idempotency';
import { RuntimeConfigService } from './runtime-config.service';

// A prepared doctor's visit (docs/backend/analysis/doctor-visit-summary.md): the period, the
// sections and the guardian's note, with its live share links.
export type VisitSummary = Schemas['VisitSummaryResponse'];
export type VisitSummaryRequest = Schemas['VisitSummaryRequest'];
export type VisitSummarySection = Schemas['VisitSummarySection'];
// The report, identical for the guardian and the doctor: each section is the owning feature's own
// response, or null when it wasn't chosen or its feature is off.
export type VisitSummaryReport = Schemas['VisitSummaryReport'];
export type VisitSummaryMedicineSection = Schemas['VisitSummaryMedicineSection'];
export type VisitSummaryWithReport = Schemas['VisitSummaryWithReportResponse'];
export type SharedVisitSummary = Schemas['SharedVisitSummaryResponse'];
// The plaintext token is only ever returned here, once.
export type CreatedVisitSummaryShareLink = Schemas['VisitSummaryShareLinkResponse'];

export const VISIT_SUMMARY_SECTIONS: readonly VisitSummarySection[] = [
  'Medicine',
  'Sleep',
  'Feelings',
];

export const SHARED_VISIT_SUMMARY_PATH = '/shared/visit-summary/';

@Injectable({ providedIn: 'root' })
export class VisitSummariesService {
  private readonly http = inject(HttpClient);
  private readonly runtimeConfig = inject(RuntimeConfigService);

  private base(childId: string): string {
    return `${this.runtimeConfig.apiBaseUrl}/visit-summaries/children/${childId}`;
  }

  list(childId: string): Promise<VisitSummary[]> {
    return firstValueFrom(this.http.get<VisitSummary[]>(this.base(childId)));
  }

  get(childId: string, summaryId: string): Promise<VisitSummaryWithReport> {
    return firstValueFrom(
      this.http.get<VisitSummaryWithReport>(`${this.base(childId)}/${summaryId}`),
    );
  }

  create(childId: string, request: VisitSummaryRequest): Promise<VisitSummary> {
    return firstValueFrom(postIdempotent<VisitSummary>(this.http, this.base(childId), request));
  }

  update(childId: string, summaryId: string, request: VisitSummaryRequest): Promise<VisitSummary> {
    return firstValueFrom(
      this.http.put<VisitSummary>(`${this.base(childId)}/${summaryId}`, request),
    );
  }

  delete(childId: string, summaryId: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.base(childId)}/${summaryId}`));
  }

  createShareLink(
    childId: string,
    summaryId: string,
    expiresAt: string | null,
  ): Promise<CreatedVisitSummaryShareLink> {
    return firstValueFrom(
      postIdempotent<CreatedVisitSummaryShareLink>(
        this.http,
        `${this.base(childId)}/${summaryId}/share-links`,
        { expiresAt },
      ),
    );
  }

  revokeShareLink(childId: string, summaryId: string, linkId: string): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>(`${this.base(childId)}/${summaryId}/share-links/${linkId}`),
    );
  }

  // Anonymous: the token is the credential, and the period is the one the guardian fixed.
  getShared(token: string): Promise<SharedVisitSummary> {
    return firstValueFrom(
      this.http.get<SharedVisitSummary>(
        `${this.runtimeConfig.apiBaseUrl}/visit-summaries/shared/${encodeURIComponent(token)}`,
      ),
    );
  }
}

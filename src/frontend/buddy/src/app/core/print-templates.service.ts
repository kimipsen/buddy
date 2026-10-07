import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { sortByName } from './array-utils';
import { postIdempotent } from './http-idempotency';
import { MealSlot } from './mealplans.service';
import { RuntimeConfigService } from './runtime-config.service';
import { Weekday } from './work-locations.service';

// PaperSize ordinals: 0 = A4, 1 = A3. Orientation is always landscape.
export type PaperSize = 0 | 1;

// PrintRowKind ordinals, matching the backend enum (new kinds are appended at the end).
export const PRINT_ROW_KIND = {
  meal: 0,
  pickup: 1,
  workLocation: 2,
  calendarMarker: 3,
  calendarEvents: 4,
  taskChecklist: 5,
  blank: 6,
} as const;
export type PrintRowKind = (typeof PRINT_ROW_KIND)[keyof typeof PRINT_ROW_KIND];

// A flat row: only the fields its kind uses are set, everything else null/false -- the backend
// rejects stray values (see docs/backend/analysis/week-plan-print-templates.md, Question 3).
export interface PrintTemplateRow {
  kind: PrintRowKind;
  label: string;
  heightWeight: number;
  childId: string | null;
  mealGroupId: string | null;
  mealSlot: MealSlot | null;
  guardianId: string | null;
  workLocationId: string | null;
  calendarIds: string[] | null;
  assignedToId: string | null;
  titleFilter: string | null;
  maxItems: number | null;
  showTime: boolean;
  showAssignee: boolean;
}

export interface GuardianColor {
  guardianId: string;
  color: string;
}

export interface PrintTemplate {
  id: string;
  ownerUserId: string | null;
  ownerGroupId: string | null;
  name: string;
  paperSize: PaperSize;
  defaultStartWeekday: Weekday;
  showWeekNumber: boolean;
  rows: PrintTemplateRow[];
  guardianColors: GuardianColor[];
}

export interface PrintTemplateSummary {
  id: string;
  ownerUserId: string | null;
  ownerGroupId: string | null;
  name: string;
}

export interface PrintTemplateLayout {
  paperSize: PaperSize;
  defaultStartWeekday: Weekday;
  showWeekNumber: boolean;
}

// A row with every kind-specific field cleared -- the starting point for building any kind.
export function emptyRow(kind: PrintRowKind, label = ''): PrintTemplateRow {
  return {
    kind,
    label,
    heightWeight: 1,
    childId: null,
    mealGroupId: null,
    mealSlot: null,
    guardianId: null,
    workLocationId: null,
    calendarIds: null,
    assignedToId: null,
    titleFilter: null,
    maxItems: null,
    showTime: false,
    showAssignee: false,
  };
}

@Injectable({ providedIn: 'root' })
export class PrintTemplatesService {
  private readonly http = inject(HttpClient);
  private readonly runtimeConfig = inject(RuntimeConfigService);

  private get base(): string {
    return `${this.runtimeConfig.apiBaseUrl}/print-templates`;
  }

  list(): Promise<PrintTemplateSummary[]> {
    return firstValueFrom(this.http.get<PrintTemplateSummary[]>(this.base)).then(sortByName);
  }

  get(templateId: string): Promise<PrintTemplate> {
    return firstValueFrom(this.http.get<PrintTemplate>(`${this.base}/${templateId}`));
  }

  // groupId set = the template is owned by that group; omitted = owned by the caller.
  create(name: string, groupId: string | null = null): Promise<PrintTemplate> {
    return firstValueFrom(postIdempotent<PrintTemplate>(this.http, this.base, { name, groupId }));
  }

  rename(templateId: string, name: string): Promise<PrintTemplate> {
    return firstValueFrom(
      this.http.patch<PrintTemplate>(`${this.base}/${templateId}/name`, { name }),
    );
  }

  updateLayout(templateId: string, layout: PrintTemplateLayout): Promise<PrintTemplate> {
    return firstValueFrom(
      this.http.patch<PrintTemplate>(`${this.base}/${templateId}/layout`, layout),
    );
  }

  replaceRows(templateId: string, rows: PrintTemplateRow[]): Promise<PrintTemplate> {
    return firstValueFrom(
      this.http.put<PrintTemplate>(`${this.base}/${templateId}/rows`, { rows }),
    );
  }

  replaceColors(templateId: string, colors: GuardianColor[]): Promise<PrintTemplate> {
    return firstValueFrom(
      this.http.put<PrintTemplate>(`${this.base}/${templateId}/colors`, { colors }),
    );
  }

  delete(templateId: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.base}/${templateId}`));
  }
}

import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { sortByName } from './array-utils';
import { postIdempotent } from './http-idempotency';
import type { Schemas } from './api/schemas';
import { RuntimeConfigService } from './runtime-config.service';

export type PaperSize = Schemas['PaperSize'];

// PrintRowKind names, for code that builds or matches rows of one kind.
export const PRINT_ROW_KIND = {
  meal: 'Meal',
  pickup: 'Pickup',
  workLocation: 'WorkLocation',
  calendarMarker: 'CalendarMarker',
  calendarEvents: 'CalendarEvents',
  taskChecklist: 'TaskChecklist',
  blank: 'Blank',
} as const satisfies Record<string, Schemas['PrintRowKind']>;

export type PrintRowKind = Schemas['PrintRowKind'];

// A flat row: only the fields its kind uses are set, everything else null/false -- the backend
// rejects stray values (see docs/backend/analysis/week-plan-print-templates.md, Question 3).
// Every field is always sent and returned (emptyRow clears the ones a kind doesn't use).
export type PrintTemplateRow = Required<Schemas['PrintTemplateRow']>;

export type GuardianColor = Schemas['GuardianColorRequest'];

// A babysitter is the (guardianId, babysitterId) pair a kind 4 pickup assignee carries.
export type BabysitterColor = Schemas['BabysitterColorRequest'];

export type PrintTemplate = Omit<Schemas['PrintTemplateResponse'], 'rows'> & {
  rows: PrintTemplateRow[];
};

export type PrintTemplateSummary = Schemas['PrintTemplateSummary'];

export type PrintTemplateLayout = Schemas['UpdatePrintTemplateLayoutRequest'];

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

  replaceBabysitterColors(templateId: string, colors: BabysitterColor[]): Promise<PrintTemplate> {
    return firstValueFrom(
      this.http.put<PrintTemplate>(`${this.base}/${templateId}/babysitter-colors`, { colors }),
    );
  }

  delete(templateId: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.base}/${templateId}`));
  }
}

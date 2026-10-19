import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { sortByName } from './array-utils';
import { postIdempotent } from './http-idempotency';
import type { Schemas } from './api/schemas';
import { RuntimeConfigService } from './runtime-config.service';

// Wire format for Duration/TotalDuration matches the backend's default System.Text.Json TimeSpan
// serialization (the "c" format: "[-][d.]hh:mm:ss[.fffffff]" -- see SubtaskResponse.Duration /
// TaskTemplateResponse.TotalDuration in CreateTaskTemplate.Endpoint.cs). The service converts to
// and from whole minutes at this boundary so components never touch the wire string directly.
function parseDurationMinutes(duration: string): number {
  // Stryker disable next-line Regex: the seconds group is unread and the pattern has no end anchor, so (\d) matches wherever (\d+) does.
  const match = /^-?(?:(\d+)\.)?(\d+):(\d+):(\d+)/.exec(duration);

  if (!match) {
    return 0;
  }

  const [, days, hours, minutes] = match;
  return (Number(days ?? 0) * 24 + Number(hours)) * 60 + Number(minutes);
}

function formatDurationMinutes(totalMinutes: number): string {
  const wholeMinutes = Math.max(0, Math.round(totalMinutes));
  const hours = Math.floor(wholeMinutes / 60);
  const minutes = wholeMinutes % 60;
  return `${String(hours).padStart(2, '0')}:${String(minutes).padStart(2, '0')}:00`;
}

export type TaskTemplateKind = Schemas['TaskTemplateKind'];
export type ChecklistSuggestionStatus = Schemas['ChecklistSuggestionStatus'];
export type ChecklistSuggestion = Schemas['ChecklistSuggestionResponse'];
type DayOfWeek = Schemas['DayOfWeek'];

export interface Subtask {
  id: string;
  title: string;
  icon: string | null;
  durationMinutes: number;
  // Empty: the item applies whenever the template occurs. Only checklists offer it in the UI.
  weekdays: DayOfWeek[];
}

// A TaskTemplate belongs to exactly one child -- it's scoped by childId in the URL because that's
// whose library it is. No group-sharing axis (TaskLibraryAccessTier has no analog to
// MealplanAccessTier's group scope).
export interface TaskTemplate {
  id: string;
  name: string;
  icon: string;
  color: string;
  subtasks: Subtask[];
  totalDurationMinutes: number;
  isArchived: boolean;
  createdBy: string;
  lastModifiedBy: string;
  kind: TaskTemplateKind;
  // A packing list's "show the evening before from" time ("HH:mm"), or null for the day only.
  // Always null for a routine.
  eveningBeforeFrom: string | null;
  // Pending first (oldest first), then the most recently resolved; always empty for a routine.
  suggestions: ChecklistSuggestion[];
}

export type TaskTemplateDetails = Pick<
  Schemas['CreateTaskTemplateRequest'],
  'name' | 'icon' | 'color'
>;

// What only creation takes: the kind is fixed from then on.
export interface NewTaskTemplate extends TaskTemplateDetails {
  kind?: TaskTemplateKind;
  eveningBeforeFrom?: string | null;
}

// The wire TimeOnly is "HH:mm:ss"; the app's time inputs use "HH:mm".
function toHourMinute(time: string | null | undefined): string | null {
  return time ? time.slice(0, 5) : null;
}

function toWireTime(time: string | null | undefined): string | null {
  return time ? `${time.slice(0, 5)}:00` : null;
}

// An empty list means "every day" and goes over the wire as null.
function toWireWeekdays(weekdays: DayOfWeek[] | undefined): DayOfWeek[] | null {
  return weekdays && weekdays.length > 0 ? weekdays : null;
}

type TaskTemplateResponse = Schemas['TaskTemplateResponse'];

function fromResponse(response: TaskTemplateResponse): TaskTemplate {
  return {
    id: response.id,
    name: response.name,
    icon: response.icon,
    color: response.color,
    subtasks: response.subtasks.map((subtask) => ({
      id: subtask.id,
      title: subtask.title,
      icon: subtask.icon,
      durationMinutes: parseDurationMinutes(subtask.duration),
      weekdays: subtask.weekdays,
    })),
    totalDurationMinutes: parseDurationMinutes(response.totalDuration),
    isArchived: response.isArchived,
    createdBy: response.createdBy,
    lastModifiedBy: response.lastModifiedBy,
    kind: response.kind,
    eveningBeforeFrom: toHourMinute(response.checklist?.eveningBeforeFrom),
    suggestions: response.suggestions,
  };
}

function totalDurationMinutesOf(subtasks: Subtask[]): number {
  return subtasks.reduce((sum, subtask) => sum + subtask.durationMinutes, 0);
}

@Injectable({ providedIn: 'root' })
export class TaskLibraryService {
  private readonly http = inject(HttpClient);
  private readonly runtimeConfig = inject(RuntimeConfigService);

  // Shared across every component reading `templates` (e.g. the task-library editor and a
  // template picker on the same page), so a create/update/archive/subtask-edit in one place is
  // reflected everywhere else immediately, without each component needing to know about the
  // others -- mirrors MealplansService.meals.
  private readonly templatesState = signal<TaskTemplate[]>([]);
  readonly templates = computed(() => sortByName(this.templatesState()));

  private base(): string {
    return `${this.runtimeConfig.apiBaseUrl}/task-templates`;
  }

  private replaceTemplate(template: TaskTemplate): void {
    this.templatesState.update((current) =>
      current.map((existing) => (existing.id === template.id ? template : existing)),
    );
  }

  // Used when the caller stops pointing at any particular child (e.g. a calendar assignee that
  // isn't one of the guardian's children) -- there's no valid childId to list templates for, so
  // the shared state should reflect "nothing selected" rather than keep showing the last child's
  // templates.
  clearTemplates(): void {
    this.templatesState.set([]);
  }

  async listTaskTemplates(childId: string): Promise<TaskTemplate[]> {
    const templates = await this.fetchTaskTemplates(childId);
    this.templatesState.set(templates);
    return templates;
  }

  // A child's templates without touching the shared `templates` state: for readers that aren't the
  // editor (the child home's suggestions, the dashboard's per-child counts), so the editor's list
  // never flips to whichever child was read last.
  async fetchTaskTemplates(childId: string): Promise<TaskTemplate[]> {
    const responses = await firstValueFrom(
      this.http.get<TaskTemplateResponse[]>(`${this.base()}/children/${childId}`),
    );
    return sortByName(responses.map(fromResponse));
  }

  async createTaskTemplate(childId: string, request: NewTaskTemplate): Promise<TaskTemplate> {
    const response = await firstValueFrom(
      postIdempotent<TaskTemplateResponse>(this.http, `${this.base()}/children/${childId}`, {
        name: request.name,
        icon: request.icon,
        color: request.color,
        kind: request.kind ?? 'Routine',
        eveningBeforeFrom: toWireTime(request.eveningBeforeFrom),
      }),
    );
    const template = fromResponse(response);
    this.templatesState.update((current) => [...current, template]);
    return template;
  }

  async updateTaskTemplate(
    templateId: string,
    request: TaskTemplateDetails,
  ): Promise<TaskTemplate> {
    const response = await firstValueFrom(
      this.http.patch<TaskTemplateResponse>(`${this.base()}/${templateId}`, request),
    );
    const template = fromResponse(response);
    this.replaceTemplate(template);
    return template;
  }

  async archiveTaskTemplate(templateId: string): Promise<void> {
    await firstValueFrom(this.http.delete<void>(`${this.base()}/${templateId}`));
    this.templatesState.update((current) =>
      current.map((template) =>
        template.id === templateId ? { ...template, isArchived: true } : template,
      ),
    );
  }

  async addSubtask(
    templateId: string,
    title: string,
    icon: string | null,
    durationMinutes: number,
    position?: number | null,
    weekdays?: DayOfWeek[],
  ): Promise<TaskTemplate> {
    const response = await firstValueFrom(
      postIdempotent<TaskTemplateResponse>(this.http, `${this.base()}/${templateId}/subtasks`, {
        title,
        icon,
        duration: formatDurationMinutes(durationMinutes),
        position: position ?? null,
        weekdays: toWireWeekdays(weekdays),
      }),
    );
    const template = fromResponse(response);
    this.replaceTemplate(template);
    return template;
  }

  async updateSubtask(
    templateId: string,
    subtaskId: string,
    title: string,
    icon: string | null,
    durationMinutes: number,
    weekdays?: DayOfWeek[],
  ): Promise<TaskTemplate> {
    const response = await firstValueFrom(
      this.http.patch<TaskTemplateResponse>(`${this.base()}/${templateId}/subtasks/${subtaskId}`, {
        title,
        icon,
        duration: formatDurationMinutes(durationMinutes),
        weekdays: toWireWeekdays(weekdays),
      }),
    );
    const template = fromResponse(response);
    this.replaceTemplate(template);
    return template;
  }

  async removeSubtask(templateId: string, subtaskId: string): Promise<void> {
    await firstValueFrom(
      this.http.delete<void>(`${this.base()}/${templateId}/subtasks/${subtaskId}`),
    );
    this.templatesState.update((current) =>
      current.map((template) => {
        if (template.id !== templateId) {
          return template;
        }

        const subtasks = template.subtasks.filter((subtask) => subtask.id !== subtaskId);
        return { ...template, subtasks, totalDurationMinutes: totalDurationMinutesOf(subtasks) };
      }),
    );
  }

  async reorderSubtasks(templateId: string, newOrder: string[]): Promise<TaskTemplate> {
    const response = await firstValueFrom(
      this.http.put<TaskTemplateResponse>(`${this.base()}/${templateId}/subtasks/order`, {
        newOrder,
      }),
    );
    const template = fromResponse(response);
    this.replaceTemplate(template);
    return template;
  }

  // A packing list's "evening before" time ("HH:mm"), or null to show it on the day only.
  async setChecklistOptions(
    templateId: string,
    eveningBeforeFrom: string | null,
  ): Promise<TaskTemplate> {
    const response = await firstValueFrom(
      this.http.put<TaskTemplateResponse>(`${this.base()}/${templateId}/checklist-options`, {
        eveningBeforeFrom: toWireTime(eveningBeforeFrom),
      }),
    );
    const template = fromResponse(response);
    this.replaceTemplate(template);
    return template;
  }

  // Copies a template into another child's library. The copy belongs to that child, so the shared
  // state (the selected child's list) is left alone unless it is that child's.
  async copyTaskTemplate(templateId: string, childId: string): Promise<TaskTemplate> {
    const response = await firstValueFrom(
      postIdempotent<TaskTemplateResponse>(this.http, `${this.base()}/${templateId}/copy`, {
        childId,
      }),
    );
    return fromResponse(response);
  }

  // The child's own packing list: suggest an item for a guardian to approve. An empty weekday list
  // means "whenever the list occurs".
  async suggestChecklistItem(
    templateId: string,
    title: string,
    weekdays: DayOfWeek[] = [],
  ): Promise<TaskTemplate> {
    const response = await firstValueFrom(
      postIdempotent<TaskTemplateResponse>(this.http, `${this.base()}/${templateId}/suggestions`, {
        title,
        weekdays: toWireWeekdays(weekdays),
      }),
    );
    return fromResponse(response);
  }

  async withdrawChecklistSuggestion(
    templateId: string,
    suggestionId: string,
  ): Promise<TaskTemplate> {
    return fromResponse(
      await firstValueFrom(
        this.http.post<TaskTemplateResponse>(
          `${this.base()}/${templateId}/suggestions/${suggestionId}/withdraw`,
          null,
        ),
      ),
    );
  }

  // The guardian's final title and days; the item is appended to the list.
  async approveChecklistSuggestion(
    templateId: string,
    suggestionId: string,
    title: string,
    weekdays: DayOfWeek[] = [],
  ): Promise<TaskTemplate> {
    const response = await firstValueFrom(
      this.http.post<TaskTemplateResponse>(
        `${this.base()}/${templateId}/suggestions/${suggestionId}/approve`,
        { title, weekdays: toWireWeekdays(weekdays), position: null },
      ),
    );
    const template = fromResponse(response);
    this.replaceTemplate(template);
    return template;
  }

  async declineChecklistSuggestion(
    templateId: string,
    suggestionId: string,
  ): Promise<TaskTemplate> {
    const response = await firstValueFrom(
      this.http.post<TaskTemplateResponse>(
        `${this.base()}/${templateId}/suggestions/${suggestionId}/decline`,
        null,
      ),
    );
    const template = fromResponse(response);
    this.replaceTemplate(template);
    return template;
  }
}

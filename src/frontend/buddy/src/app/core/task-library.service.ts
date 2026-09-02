import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { postIdempotent } from './http-idempotency';
import { RuntimeConfigService } from './runtime-config.service';

// Wire format for Duration/TotalDuration matches the backend's default System.Text.Json TimeSpan
// serialization (the "c" format: "[-][d.]hh:mm:ss[.fffffff]" -- see SubtaskResponse.Duration /
// TaskTemplateResponse.TotalDuration in CreateTaskTemplate.Endpoint.cs). The service converts to
// and from whole minutes at this boundary so components never touch the wire string directly.
function parseDurationMinutes(duration: string): number {
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

export interface Subtask {
  id: string;
  title: string;
  icon: string | null;
  durationMinutes: number;
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
}

export interface TaskTemplateDetails {
  name: string;
  icon: string;
  color: string;
}

interface SubtaskResponse {
  id: string;
  title: string;
  icon: string | null;
  duration: string;
}

interface TaskTemplateResponse {
  id: string;
  name: string;
  icon: string;
  color: string;
  subtasks: SubtaskResponse[];
  totalDuration: string;
  isArchived: boolean;
  createdBy: string;
  lastModifiedBy: string;
}

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
      durationMinutes: parseDurationMinutes(subtask.duration)
    })),
    totalDurationMinutes: parseDurationMinutes(response.totalDuration),
    isArchived: response.isArchived,
    createdBy: response.createdBy,
    lastModifiedBy: response.lastModifiedBy
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
  readonly templates = this.templatesState.asReadonly();

  private base(): string {
    return `${this.runtimeConfig.apiBaseUrl}/task-templates`;
  }

  private replaceTemplate(template: TaskTemplate): void {
    this.templatesState.update((current) => current.map((existing) => (existing.id === template.id ? template : existing)));
  }

  // Used when the caller stops pointing at any particular child (e.g. a calendar assignee that
  // isn't one of the guardian's children) -- there's no valid childId to list templates for, so
  // the shared state should reflect "nothing selected" rather than keep showing the last child's
  // templates.
  clearTemplates(): void {
    this.templatesState.set([]);
  }

  async listTaskTemplates(childId: string): Promise<TaskTemplate[]> {
    const responses = await firstValueFrom(this.http.get<TaskTemplateResponse[]>(`${this.base()}/children/${childId}`));
    const templates = responses.map(fromResponse);
    this.templatesState.set(templates);
    return templates;
  }

  async createTaskTemplate(childId: string, request: TaskTemplateDetails): Promise<TaskTemplate> {
    const response = await firstValueFrom(
      postIdempotent<TaskTemplateResponse>(this.http, `${this.base()}/children/${childId}`, request)
    );
    const template = fromResponse(response);
    this.templatesState.update((current) => [...current, template]);
    return template;
  }

  async updateTaskTemplate(templateId: string, request: TaskTemplateDetails): Promise<TaskTemplate> {
    const response = await firstValueFrom(this.http.patch<TaskTemplateResponse>(`${this.base()}/${templateId}`, request));
    const template = fromResponse(response);
    this.replaceTemplate(template);
    return template;
  }

  async archiveTaskTemplate(templateId: string): Promise<void> {
    await firstValueFrom(this.http.delete<void>(`${this.base()}/${templateId}`));
    this.templatesState.update((current) =>
      current.map((template) => (template.id === templateId ? { ...template, isArchived: true } : template))
    );
  }

  async addSubtask(templateId: string, title: string, icon: string | null, durationMinutes: number, position?: number | null): Promise<TaskTemplate> {
    const response = await firstValueFrom(
      postIdempotent<TaskTemplateResponse>(this.http, `${this.base()}/${templateId}/subtasks`, {
        title,
        icon,
        duration: formatDurationMinutes(durationMinutes),
        position: position ?? null
      })
    );
    const template = fromResponse(response);
    this.replaceTemplate(template);
    return template;
  }

  async updateSubtask(templateId: string, subtaskId: string, title: string, icon: string | null, durationMinutes: number): Promise<TaskTemplate> {
    const response = await firstValueFrom(
      this.http.patch<TaskTemplateResponse>(`${this.base()}/${templateId}/subtasks/${subtaskId}`, {
        title,
        icon,
        duration: formatDurationMinutes(durationMinutes)
      })
    );
    const template = fromResponse(response);
    this.replaceTemplate(template);
    return template;
  }

  async removeSubtask(templateId: string, subtaskId: string): Promise<void> {
    await firstValueFrom(this.http.delete<void>(`${this.base()}/${templateId}/subtasks/${subtaskId}`));
    this.templatesState.update((current) =>
      current.map((template) => {
        if (template.id !== templateId) {
          return template;
        }

        const subtasks = template.subtasks.filter((subtask) => subtask.id !== subtaskId);
        return { ...template, subtasks, totalDurationMinutes: totalDurationMinutesOf(subtasks) };
      })
    );
  }

  async reorderSubtasks(templateId: string, newOrder: string[]): Promise<TaskTemplate> {
    const response = await firstValueFrom(this.http.put<TaskTemplateResponse>(`${this.base()}/${templateId}/subtasks/order`, { newOrder }));
    const template = fromResponse(response);
    this.replaceTemplate(template);
    return template;
  }
}

import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { ChildSummary, GuardiansService } from '../../../../core/guardians.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { Subtask, TaskLibraryService, TaskTemplate } from '../../../../core/task-library.service';

const DEFAULT_COLOR = '#6366f1';
const DEFAULT_ICON = '📋';
const DEFAULT_SUBTASK_DURATION_MINUTES = 5;

// Formats a whole-minutes duration for display (e.g. "35m", "1h", "1h 30m") -- distinct from the
// wire "c"-format TimeSpan string TaskLibraryService already converts away from.
function formatDuration(totalMinutes: number): string {
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;

  if (hours === 0) {
    return `${minutes}m`;
  }

  return minutes === 0 ? `${hours}h` : `${hours}h ${minutes}m`;
}

// Self-contained: loads its own linked children and picks the first automatically, same shape as
// ManageMedicines -- unlike ManageMeals/MealplansService, TaskLibraryAccessTier has no
// group-sharing axis (see TaskLibraryAuthorization.cs), so there's no scope input to accept here.
@Component({
  selector: 'app-manage-tasks',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './manage-tasks.html'
})
export class ManageTasks implements OnInit {
  private readonly guardians = inject(GuardiansService);
  private readonly taskLibrary = inject(TaskLibraryService);

  protected readonly hasChildren = signal(true);
  protected readonly children = signal<ChildSummary[]>([]);
  protected readonly selectedChildId = signal<string | null>(null);

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  // Reads straight from the shared service state, so a create/archive/subtask-edit from anywhere
  // else on the page (there's nowhere else yet, but mirrors ManageMeals's contract) shows up here
  // without a manual refetch. Archived templates stay in the list (visually distinguished) rather
  // than being filtered out, unlike ManageMeals -- a template can't be un-archived once fetched,
  // so keeping it visible with a badge lets a guardian see what happened rather than having it
  // silently vanish.
  protected readonly templates = this.taskLibrary.templates;

  protected readonly expandedTemplateId = signal<string | null>(null);

  protected readonly newTemplateName = signal('');
  protected readonly newTemplateIcon = signal(DEFAULT_ICON);
  protected readonly newTemplateColor = signal(DEFAULT_COLOR);
  protected readonly creating = signal(false);
  protected readonly createError = signal<string | null>(null);

  protected readonly archivingTemplateId = signal<string | null>(null);

  protected readonly editingTemplateId = signal<string | null>(null);
  protected readonly editTemplateName = signal('');
  protected readonly editTemplateIcon = signal('');
  protected readonly editTemplateColor = signal(DEFAULT_COLOR);
  protected readonly savingTemplateId = signal<string | null>(null);
  protected readonly templateError = signal<string | null>(null);

  protected readonly newSubtaskTitle = signal('');
  protected readonly newSubtaskIcon = signal('');
  protected readonly newSubtaskDuration = signal(DEFAULT_SUBTASK_DURATION_MINUTES);
  protected readonly addingSubtask = signal(false);
  protected readonly subtaskError = signal<string | null>(null);

  protected readonly editingSubtaskId = signal<string | null>(null);
  protected readonly editSubtaskTitle = signal('');
  protected readonly editSubtaskIcon = signal('');
  protected readonly editSubtaskDuration = signal(DEFAULT_SUBTASK_DURATION_MINUTES);
  protected readonly savingSubtaskId = signal<string | null>(null);

  protected readonly removingSubtaskId = signal<string | null>(null);
  protected readonly reorderingTemplateId = signal<string | null>(null);

  ngOnInit(): void {
    void this.loadChildren();
  }

  protected formatDuration(totalMinutes: number): string {
    return formatDuration(totalMinutes);
  }

  protected async onChildChange(childId: string): Promise<void> {
    this.selectedChildId.set(childId);
    this.expandedTemplateId.set(null);
    await this.loadTemplates(childId);
  }

  protected toggleExpanded(templateId: string): void {
    this.expandedTemplateId.set(this.expandedTemplateId() === templateId ? null : templateId);
    this.subtaskError.set(null);
    this.cancelEditSubtask();
    this.cancelEditTemplate();
    this.newSubtaskTitle.set('');
    this.newSubtaskIcon.set('');
    this.newSubtaskDuration.set(DEFAULT_SUBTASK_DURATION_MINUTES);
  }

  protected startEditTemplate(template: TaskTemplate): void {
    this.editingTemplateId.set(template.id);
    this.editTemplateName.set(template.name);
    this.editTemplateIcon.set(template.icon);
    this.editTemplateColor.set(template.color);
    this.templateError.set(null);
  }

  protected cancelEditTemplate(): void {
    this.editingTemplateId.set(null);
  }

  protected async saveTemplate(templateId: string): Promise<void> {
    const name = this.editTemplateName().trim();
    const icon = this.editTemplateIcon().trim();
    const color = this.editTemplateColor().trim();

    if (!name || !icon) {
      return;
    }

    this.savingTemplateId.set(templateId);
    this.templateError.set(null);

    try {
      await this.taskLibrary.updateTaskTemplate(templateId, { name, icon, color });
      this.editingTemplateId.set(null);
    } catch {
      this.templateError.set('taskLibrary.manageTasks.form.updateError');
    } finally {
      this.savingTemplateId.set(null);
    }
  }

  protected async createTemplate(): Promise<void> {
    const childId = this.selectedChildId();
    const name = this.newTemplateName().trim();
    const icon = this.newTemplateIcon().trim();
    const color = this.newTemplateColor().trim();

    if (!childId || !name || !icon || !color) {
      return;
    }

    this.creating.set(true);
    this.createError.set(null);

    try {
      const created = await this.taskLibrary.createTaskTemplate(childId, { name, icon, color });
      this.newTemplateName.set('');
      this.newTemplateIcon.set(DEFAULT_ICON);
      this.newTemplateColor.set(DEFAULT_COLOR);
      this.expandedTemplateId.set(created.id);
    } catch {
      this.createError.set('taskLibrary.manageTasks.form.createError');
    } finally {
      this.creating.set(false);
    }
  }

  protected async archiveTemplate(templateId: string): Promise<void> {
    this.archivingTemplateId.set(templateId);
    this.error.set(null);

    try {
      await this.taskLibrary.archiveTaskTemplate(templateId);
    } catch {
      this.error.set('taskLibrary.manageTasks.archiveError');
    } finally {
      this.archivingTemplateId.set(null);
    }
  }

  protected async addSubtask(templateId: string): Promise<void> {
    const title = this.newSubtaskTitle().trim();
    const icon = this.newSubtaskIcon().trim();
    const durationMinutes = this.newSubtaskDuration();

    if (!title || durationMinutes <= 0) {
      return;
    }

    this.addingSubtask.set(true);
    this.subtaskError.set(null);

    try {
      await this.taskLibrary.addSubtask(templateId, title, icon || null, durationMinutes);
      this.newSubtaskTitle.set('');
      this.newSubtaskIcon.set('');
      this.newSubtaskDuration.set(DEFAULT_SUBTASK_DURATION_MINUTES);
    } catch {
      this.subtaskError.set('taskLibrary.manageTasks.subtasks.addError');
    } finally {
      this.addingSubtask.set(false);
    }
  }

  protected startEditSubtask(subtask: Subtask): void {
    this.editingSubtaskId.set(subtask.id);
    this.editSubtaskTitle.set(subtask.title);
    this.editSubtaskIcon.set(subtask.icon ?? '');
    this.editSubtaskDuration.set(subtask.durationMinutes);
    this.subtaskError.set(null);
  }

  protected cancelEditSubtask(): void {
    this.editingSubtaskId.set(null);
  }

  protected async saveSubtask(templateId: string, subtaskId: string): Promise<void> {
    const title = this.editSubtaskTitle().trim();
    const icon = this.editSubtaskIcon().trim();
    const durationMinutes = this.editSubtaskDuration();

    if (!title || durationMinutes <= 0) {
      return;
    }

    this.savingSubtaskId.set(subtaskId);
    this.subtaskError.set(null);

    try {
      await this.taskLibrary.updateSubtask(templateId, subtaskId, title, icon || null, durationMinutes);
      this.editingSubtaskId.set(null);
    } catch {
      this.subtaskError.set('taskLibrary.manageTasks.subtasks.updateError');
    } finally {
      this.savingSubtaskId.set(null);
    }
  }

  protected async removeSubtask(templateId: string, subtaskId: string): Promise<void> {
    this.removingSubtaskId.set(subtaskId);
    this.subtaskError.set(null);

    try {
      await this.taskLibrary.removeSubtask(templateId, subtaskId);
    } catch {
      this.subtaskError.set('taskLibrary.manageTasks.subtasks.removeError');
    } finally {
      this.removingSubtaskId.set(null);
    }
  }

  // Simplest correct v1 reorder: swap the target row with its neighbor and submit the whole
  // resulting id order -- no drag-and-drop precedent exists elsewhere in this codebase to reuse.
  protected async moveSubtask(template: TaskTemplate, index: number, direction: -1 | 1): Promise<void> {
    const targetIndex = index + direction;

    if (targetIndex < 0 || targetIndex >= template.subtasks.length) {
      return;
    }

    const order = template.subtasks.map((subtask) => subtask.id);
    [order[index], order[targetIndex]] = [order[targetIndex], order[index]];

    this.reorderingTemplateId.set(template.id);
    this.subtaskError.set(null);

    try {
      await this.taskLibrary.reorderSubtasks(template.id, order);
    } catch {
      this.subtaskError.set('taskLibrary.manageTasks.subtasks.reorderError');
    } finally {
      this.reorderingTemplateId.set(null);
    }
  }

  private async loadChildren(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      const children = await this.guardians.listMyChildren();

      if (children.length === 0) {
        this.hasChildren.set(false);
        return;
      }

      this.hasChildren.set(true);
      this.children.set(children);
      this.selectedChildId.set(children[0].id);
      await this.loadTemplates(children[0].id);
    } catch {
      this.error.set('taskLibrary.manageTasks.loadError');
    } finally {
      this.loading.set(false);
    }
  }

  private async loadTemplates(childId: string): Promise<void> {
    await this.taskLibrary.listTaskTemplates(childId);
  }
}

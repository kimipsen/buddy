import { Component, computed, inject, linkedSignal, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { swapped } from '../../../../core/array-utils';
import { GuardiansService } from '../../../../core/guardians.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { Subtask, TaskLibraryService, TaskTemplate } from '../../../../core/task-library.service';
import { createAction } from '../../../../shared/action-state/action-state';
import { ColorSwatchPicker } from '../../../../shared/color-swatch-picker/color-swatch-picker';
import { Stepper } from '../../../../shared/stepper/stepper';

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
  imports: [FormsModule, TranslatePipe, ColorSwatchPicker, Stepper],
  templateUrl: './manage-tasks.html',
})
export class ManageTasks {
  private readonly guardians = inject(GuardiansService);
  private readonly taskLibrary = inject(TaskLibraryService);

  protected readonly children = resource({ loader: () => this.guardians.listMyChildren() });
  protected readonly childList = computed(() =>
    this.children.hasValue() ? this.children.value() : [],
  );
  protected readonly selectedChildId = linkedSignal(() => this.childList()[0]?.id);

  // Fetches the selected child's templates into the shared service state (read via templates
  // below); idle until a child is selected.
  private readonly templatesLoad = resource({
    params: () => {
      const childId = this.selectedChildId();
      return childId ? { childId } : undefined;
    },
    loader: ({ params }) => this.taskLibrary.listTaskTemplates(params.childId),
  });

  protected readonly loading = computed(
    () => this.children.isLoading() || this.templatesLoad.isLoading(),
  );
  protected readonly hasChildren = computed(
    () => !this.children.hasValue() || this.children.value().length > 0,
  );
  protected readonly loadFailed = computed(
    () => !!this.children.error() || !!this.templatesLoad.error(),
  );

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
  protected readonly creating = createAction();

  protected readonly archiving = createAction<string>();

  protected readonly editingTemplateId = signal<string | null>(null);
  protected readonly editTemplateName = signal('');
  protected readonly editTemplateIcon = signal('');
  protected readonly editTemplateColor = signal(DEFAULT_COLOR);
  protected readonly savingTemplate = createAction<string>();

  protected readonly newSubtaskTitle = signal('');
  protected readonly newSubtaskIcon = signal('');
  protected readonly newSubtaskDuration = signal(DEFAULT_SUBTASK_DURATION_MINUTES);

  protected readonly editingSubtaskId = signal<string | null>(null);
  protected readonly editSubtaskTitle = signal('');
  protected readonly editSubtaskIcon = signal('');
  protected readonly editSubtaskDuration = signal(DEFAULT_SUBTASK_DURATION_MINUTES);

  // Every subtask operation shares one error line, so they share one action. Ids: 'add',
  // 'save:<subtaskId>', 'remove:<subtaskId>', 'reorder:<templateId>'.
  protected readonly subtasks = createAction<string>();

  protected formatDuration(totalMinutes: number): string {
    return formatDuration(totalMinutes);
  }

  protected onChildChange(childId: string): void {
    this.selectedChildId.set(childId);
    this.expandedTemplateId.set(null);
  }

  protected toggleExpanded(templateId: string): void {
    this.expandedTemplateId.set(this.expandedTemplateId() === templateId ? null : templateId);
    this.subtasks.clearError();
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
    this.savingTemplate.clearError();
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

    await this.savingTemplate.run(
      templateId,
      async () => {
        await this.taskLibrary.updateTaskTemplate(templateId, { name, icon, color });
        this.editingTemplateId.set(null);
      },
      'taskLibrary.manageTasks.form.updateError',
    );
  }

  protected async createTemplate(): Promise<void> {
    const childId = this.selectedChildId();
    const name = this.newTemplateName().trim();
    const icon = this.newTemplateIcon().trim();
    const color = this.newTemplateColor().trim();

    if (!childId || !name || !icon || !color) {
      return;
    }

    await this.creating.run(
      true,
      async () => {
        const created = await this.taskLibrary.createTaskTemplate(childId, { name, icon, color });
        this.newTemplateName.set('');
        this.newTemplateIcon.set(DEFAULT_ICON);
        this.newTemplateColor.set(DEFAULT_COLOR);
        // Switched child while creating: the service appended the previous child's template to
        // the new child's list, so refetch that list instead of expanding the stray template.
        if (this.selectedChildId() !== childId) {
          this.templatesLoad.reload();
          return;
        }
        this.expandedTemplateId.set(created.id);
      },
      'taskLibrary.manageTasks.form.createError',
    );
  }

  protected async archiveTemplate(templateId: string): Promise<void> {
    await this.archiving.run(
      templateId,
      async () => {
        await this.taskLibrary.archiveTaskTemplate(templateId);
      },
      'taskLibrary.manageTasks.archiveError',
    );
  }

  protected async addSubtask(templateId: string): Promise<void> {
    const title = this.newSubtaskTitle().trim();
    const icon = this.newSubtaskIcon().trim();
    const durationMinutes = this.newSubtaskDuration();

    if (!title || durationMinutes <= 0) {
      return;
    }

    await this.subtasks.run(
      'add',
      async () => {
        await this.taskLibrary.addSubtask(templateId, title, icon || null, durationMinutes);
        this.newSubtaskTitle.set('');
        this.newSubtaskIcon.set('');
        this.newSubtaskDuration.set(DEFAULT_SUBTASK_DURATION_MINUTES);
      },
      'taskLibrary.manageTasks.subtasks.addError',
    );
  }

  protected startEditSubtask(subtask: Subtask): void {
    this.editingSubtaskId.set(subtask.id);
    this.editSubtaskTitle.set(subtask.title);
    this.editSubtaskIcon.set(subtask.icon ?? '');
    this.editSubtaskDuration.set(subtask.durationMinutes);
    this.subtasks.clearError();
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

    await this.subtasks.run(
      `save:${subtaskId}`,
      async () => {
        await this.taskLibrary.updateSubtask(
          templateId,
          subtaskId,
          title,
          icon || null,
          durationMinutes,
        );
        this.editingSubtaskId.set(null);
      },
      'taskLibrary.manageTasks.subtasks.updateError',
    );
  }

  protected async removeSubtask(templateId: string, subtaskId: string): Promise<void> {
    await this.subtasks.run(
      `remove:${subtaskId}`,
      async () => {
        await this.taskLibrary.removeSubtask(templateId, subtaskId);
      },
      'taskLibrary.manageTasks.subtasks.removeError',
    );
  }

  // Simplest correct v1 reorder: swap the target row with its neighbor and submit the whole
  // resulting id order -- no drag-and-drop precedent exists elsewhere in this codebase to reuse.
  protected async moveSubtask(
    template: TaskTemplate,
    index: number,
    direction: -1 | 1,
  ): Promise<void> {
    const targetIndex = index + direction;

    if (targetIndex < 0 || targetIndex >= template.subtasks.length) {
      return;
    }

    const order = swapped(
      template.subtasks.map((subtask) => subtask.id),
      index,
      targetIndex,
    );

    await this.subtasks.run(
      `reorder:${template.id}`,
      async () => {
        await this.taskLibrary.reorderSubtasks(template.id, order);
      },
      'taskLibrary.manageTasks.subtasks.reorderError',
    );
  }
}

import { Component, computed, inject, linkedSignal, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';

import { swapped } from '../../../../core/array-utils';
import { DayOfWeek } from '../../../../core/date-utils';
import { GuardiansService } from '../../../../core/guardians.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import {
  Subtask,
  TaskLibraryService,
  TaskTemplate,
  TaskTemplateKind,
} from '../../../../core/task-library.service';
import { createAction } from '../../../../shared/action-state/action-state';
import { ColorSwatchPicker } from '../../../../shared/color-swatch-picker/color-swatch-picker';
import { SegmentedControl } from '../../../../shared/segmented-control/segmented-control';
import { Stepper } from '../../../../shared/stepper/stepper';
import { TimeSelect } from '../../../../shared/time-select/time-select';
import { Toggle } from '../../../../shared/toggle/toggle';
import { WeekdayPicker, weekdayShortKey } from '../../../../shared/weekday-picker/weekday-picker';
import { Card } from '../../../../shared/card/card';
import { SuggestionPanel } from './suggestion-panel/suggestion-panel';
import { Icon } from '../../../../shared/icon/icon';
import { IconPicker } from '../../../../shared/icon-picker/icon-picker';

const DEFAULT_COLOR = '#6366f1';
const DEFAULT_ICON = '📋';
const DEFAULT_SUBTASK_DURATION_MINUTES = 5;
// The usual "pack the bag at night" time, offered when a guardian turns the evening view on.
const DEFAULT_EVENING_BEFORE_FROM = '17:00';
const CHECKLIST: TaskTemplateKind = 'Checklist';

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
  imports: [
    IconPicker,
    Icon,
    FormsModule,
    TranslatePipe,
    ColorSwatchPicker,
    Stepper,
    Card,
    SegmentedControl,
    Toggle,
    TimeSelect,
    WeekdayPicker,
    SuggestionPanel,
  ],
  templateUrl: './manage-tasks.html',
})
export class ManageTasks {
  private readonly guardians = inject(GuardiansService);
  private readonly taskLibrary = inject(TaskLibraryService);
  private readonly translation = inject(TranslationService);
  // ?child=<id> preselects a child (the dashboard's "Packing suggestions" and evening-card links),
  // as ManageRewards does. Optional so the card also works outside a routed page.
  private readonly requestedChildId =
    inject(ActivatedRoute, { optional: true })?.snapshot.queryParamMap.get('child') ?? undefined;

  protected readonly children = resource({ loader: () => this.guardians.listMyChildren() });
  protected readonly childList = computed(() =>
    this.children.hasValue() ? this.children.value() : [],
  );
  protected readonly selectedChildId = linkedSignal(() => {
    const children = this.childList();
    return (children.find((child) => child.id === this.requestedChildId) ?? children[0])?.id;
  });
  protected readonly selectedChildName = computed(
    () =>
      this.childList().find((child) => child.id === this.selectedChildId())?.name.givenName ?? '',
  );
  // The children a template can be copied to: every other linked child.
  protected readonly copyTargets = computed(() =>
    this.childList().filter((child) => child.id !== this.selectedChildId()),
  );

  // Fetches the selected child's templates into the shared service state (read via templates
  // below); idle until a child is selected.
  private readonly templatesLoad = resource({
    params: () => {
      const childId = this.selectedChildId();
      return childId ? { childId } : undefined;
    },
    loader: async ({ params }) => {
      const templates = await this.taskLibrary.listTaskTemplates(params.childId);
      this.expandPendingSuggestions(params.childId, templates);
      return templates;
    },
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
  // Arriving from the dashboard's "Packing suggestions" pill (?child=) opens the first list with a
  // suggestion waiting, once, so the guardian lands on the panel.
  private suggestionsExpanded = false;

  protected readonly newTemplateName = signal('');
  protected readonly newTemplateIcon = signal(DEFAULT_ICON);
  protected readonly newTemplateColor = signal(DEFAULT_COLOR);
  protected readonly newTemplateKind = signal<TaskTemplateKind>('Routine');
  protected readonly kindOptions = computed(() => [
    {
      value: 'Routine' as TaskTemplateKind,
      label: this.translation.translate('taskLibrary.manageTasks.kind.routine'),
    },
    {
      value: CHECKLIST,
      label: this.translation.translate('taskLibrary.manageTasks.kind.checklist'),
    },
  ]);
  protected readonly creating = createAction();

  // A packing list's "show the evening before" switch and time.
  protected readonly savingOptions = createAction<string>();

  // "Copy to..." picker: the template it is open for, and the last copy made (for the message).
  protected readonly copyingTemplateId = signal<string | null>(null);
  protected readonly copyTargetId = signal('');
  protected readonly copiedTo = signal<{ templateId: string; name: string } | null>(null);
  protected readonly copying = createAction<string>();

  protected readonly archiving = createAction<string>();

  protected readonly editingTemplateId = signal<string | null>(null);
  protected readonly editTemplateName = signal('');
  protected readonly editTemplateIcon = signal('');
  protected readonly editTemplateColor = signal(DEFAULT_COLOR);
  protected readonly savingTemplate = createAction<string>();

  protected readonly newSubtaskTitle = signal('');
  protected readonly newSubtaskIcon = signal('');
  protected readonly newSubtaskDuration = signal(DEFAULT_SUBTASK_DURATION_MINUTES);
  protected readonly newSubtaskWeekdays = signal<DayOfWeek[]>([]);

  protected readonly editingSubtaskId = signal<string | null>(null);
  protected readonly editSubtaskTitle = signal('');
  protected readonly editSubtaskIcon = signal('');
  protected readonly editSubtaskDuration = signal(DEFAULT_SUBTASK_DURATION_MINUTES);
  protected readonly editSubtaskWeekdays = signal<DayOfWeek[]>([]);

  // Every subtask operation shares one error line, so they share one action. Ids: 'add',
  // 'save:<subtaskId>', 'remove:<subtaskId>', 'reorder:<templateId>'.
  protected readonly subtasks = createAction<string>();

  protected readonly checklistKind = CHECKLIST;
  protected readonly shortKey = weekdayShortKey;

  protected formatDuration(totalMinutes: number): string {
    return formatDuration(totalMinutes);
  }

  protected isChecklist(template: TaskTemplate): boolean {
    return template.kind === CHECKLIST;
  }

  protected onChildChange(childId: string): void {
    this.selectedChildId.set(childId);
    this.expandedTemplateId.set(null);
    this.closeCopy();
    this.copiedTo.set(null);
  }

  private expandPendingSuggestions(childId: string, templates: TaskTemplate[]): void {
    if (this.suggestionsExpanded || childId !== this.requestedChildId) {
      return;
    }

    this.suggestionsExpanded = true;
    const withSuggestions = templates.find((template) =>
      template.suggestions.some((suggestion) => suggestion.status === 'Pending'),
    );

    if (withSuggestions) {
      this.expandedTemplateId.set(withSuggestions.id);
    }
  }

  protected toggleExpanded(templateId: string): void {
    this.expandedTemplateId.set(this.expandedTemplateId() === templateId ? null : templateId);
    this.subtasks.clearError();
    this.cancelEditSubtask();
    this.cancelEditTemplate();
    this.newSubtaskTitle.set('');
    this.newSubtaskIcon.set('');
    this.newSubtaskDuration.set(DEFAULT_SUBTASK_DURATION_MINUTES);
    this.newSubtaskWeekdays.set([]);
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
    const kind = this.newTemplateKind();

    if (!childId || !name || !icon || !color) {
      return;
    }

    await this.creating.run(
      true,
      async () => {
        const created = await this.taskLibrary.createTaskTemplate(childId, {
          name,
          icon,
          color,
          kind,
          // A new packing list starts with the evening view on; the guardian can turn it off.
          eveningBeforeFrom: kind === CHECKLIST ? DEFAULT_EVENING_BEFORE_FROM : null,
        });
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

  // A checklist item has no duration and may apply on some weekdays only; a routine step has a
  // duration and no weekdays (hidden for routines in v1, docs/backend/analysis/packing-checklists.md).
  protected async addSubtask(template: TaskTemplate): Promise<void> {
    const checklist = this.isChecklist(template);
    const title = this.newSubtaskTitle().trim();
    const icon = this.newSubtaskIcon().trim();
    const durationMinutes = checklist ? 0 : this.newSubtaskDuration();

    if (!title || (!checklist && durationMinutes <= 0)) {
      return;
    }

    await this.subtasks.run(
      'add',
      async () => {
        await this.taskLibrary.addSubtask(
          template.id,
          title,
          icon || null,
          durationMinutes,
          null,
          checklist ? this.newSubtaskWeekdays() : [],
        );
        this.newSubtaskTitle.set('');
        this.newSubtaskIcon.set('');
        this.newSubtaskDuration.set(DEFAULT_SUBTASK_DURATION_MINUTES);
        this.newSubtaskWeekdays.set([]);
      },
      'taskLibrary.manageTasks.subtasks.addError',
    );
  }

  protected startEditSubtask(subtask: Subtask): void {
    this.editingSubtaskId.set(subtask.id);
    this.editSubtaskTitle.set(subtask.title);
    this.editSubtaskIcon.set(subtask.icon ?? '');
    this.editSubtaskDuration.set(subtask.durationMinutes);
    this.editSubtaskWeekdays.set([...subtask.weekdays]);
    this.subtasks.clearError();
  }

  protected cancelEditSubtask(): void {
    this.editingSubtaskId.set(null);
  }

  protected async saveSubtask(template: TaskTemplate, subtaskId: string): Promise<void> {
    const checklist = this.isChecklist(template);
    const title = this.editSubtaskTitle().trim();
    const icon = this.editSubtaskIcon().trim();
    const durationMinutes = checklist ? 0 : this.editSubtaskDuration();

    if (!title || (!checklist && durationMinutes <= 0)) {
      return;
    }

    await this.subtasks.run(
      `save:${subtaskId}`,
      async () => {
        await this.taskLibrary.updateSubtask(
          template.id,
          subtaskId,
          title,
          icon || null,
          durationMinutes,
          checklist ? this.editSubtaskWeekdays() : [],
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

  // Turning the evening view on starts from 17:00; off shows the list on the day only.
  protected async setEveningBefore(
    template: TaskTemplate,
    eveningBeforeFrom: string | null,
  ): Promise<void> {
    if (eveningBeforeFrom === template.eveningBeforeFrom) {
      return;
    }

    await this.savingOptions.run(
      template.id,
      async () => {
        await this.taskLibrary.setChecklistOptions(template.id, eveningBeforeFrom);
      },
      'taskLibrary.manageTasks.checklist.saveError',
    );
  }

  protected toggleEveningBefore(template: TaskTemplate, on: boolean): Promise<void> {
    return this.setEveningBefore(template, on ? DEFAULT_EVENING_BEFORE_FROM : null);
  }

  protected openCopy(template: TaskTemplate): void {
    this.copyingTemplateId.set(template.id);
    this.copyTargetId.set(this.copyTargets()[0]?.id ?? '');
    this.copiedTo.set(null);
    this.copying.clearError();
  }

  protected closeCopy(): void {
    this.copyingTemplateId.set(null);
  }

  // The copy lands in the other child's library; this list (the selected child's) is unchanged.
  protected async copyTemplate(template: TaskTemplate): Promise<void> {
    const target = this.copyTargets().find((child) => child.id === this.copyTargetId());

    if (!target) {
      return;
    }

    await this.copying.run(
      template.id,
      async () => {
        await this.taskLibrary.copyTaskTemplate(template.id, target.id);
        this.copyingTemplateId.set(null);
        this.copiedTo.set({ templateId: template.id, name: target.name.givenName });
      },
      'taskLibrary.manageTasks.copy.error',
    );
  }
}

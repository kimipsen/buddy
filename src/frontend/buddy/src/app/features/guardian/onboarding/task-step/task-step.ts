import {
  Component,
  computed,
  inject,
  input,
  linkedSignal,
  output,
  signal,
  untracked,
} from '@angular/core';
import { FormsModule } from '@angular/forms';

import { CalendarsService } from '../../../../core/calendars.service';
import { todayIsoDate } from '../../../../core/date-utils';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import {
  OnboardingSetup,
  ROUTINE_LOOKAHEAD_DAYS,
  isWithinDaysAhead,
} from '../../../../core/onboarding.service';
import { TaskLibraryService } from '../../../../core/task-library.service';
import { createAction } from '../../../../shared/action-state/action-state';
import {
  ColorSwatchPicker,
  DEFAULT_COLOR_SWATCHES,
} from '../../../../shared/color-swatch-picker/color-swatch-picker';
import { DateSelect } from '../../../../shared/date-select/date-select';
import { RepeatableRow } from '../../../../shared/repeatable-row/repeatable-row';
import { Stepper } from '../../../../shared/stepper/stepper';
import { TimeSelect } from '../../../../shared/time-select/time-select';

// A routine needs at least two steps to show as one on the calendar.
export const MIN_SUBTASKS = 2;
const DEFAULT_ICON = '📋';
const DEFAULT_COLOR = DEFAULT_COLOR_SWATCHES[4];
const DEFAULT_MINUTES = 5;
const DEFAULT_START_TIME = '07:30';
const NEW_TEMPLATE = 'new';

interface SubtaskRow {
  title: string;
  minutes: number;
}

function blankRows(count: number): SubtaskRow[] {
  return Array.from({ length: count }, () => ({ title: '', minutes: DEFAULT_MINUTES }));
}

// Step 5: one child's routine template with timed steps, scheduled once on the setup calendar. The
// template, its steps and the scheduling are separate writes: a failure part-way keeps what was
// saved and the retry continues from there instead of creating another template.
@Component({
  selector: 'app-onboarding-task-step',
  imports: [
    FormsModule,
    TranslatePipe,
    ColorSwatchPicker,
    DateSelect,
    RepeatableRow,
    Stepper,
    TimeSelect,
  ],
  templateUrl: './task-step.html',
})
export class TaskStep {
  private readonly taskLibrary = inject(TaskLibraryService);
  private readonly calendars = inject(CalendarsService);

  readonly setup = input.required<OnboardingSetup>();
  readonly changed = output<void>();

  protected readonly newTemplate = NEW_TEMPLATE;
  protected readonly minSubtasks = MIN_SUBTASKS;

  // Kept across reloads of the setup while the chosen child is still in it.
  protected readonly childId = linkedSignal<string, string>({
    source: () =>
      this.setup()
        .children.map((child) => child.id)
        .join(),
    computation: (_, previous) => {
      const ids = this.setup().children.map((child) => child.id);
      return previous && ids.includes(previous.value) ? previous.value : (ids[0] ?? '');
    },
  });

  protected readonly childTemplates = computed(() =>
    this.setup()
      .templates.filter((entry) => entry.childId === this.childId())
      .map((entry) => entry.template),
  );

  // One existing template is the obvious choice; with several the guardian picks (never guessed
  // from names); with none, a new one. A choice survives reloads of the setup for the same child.
  protected readonly templateChoice = linkedSignal<
    { childId: string; templateIds: string[] },
    string
  >({
    source: () => ({
      childId: this.childId(),
      templateIds: this.childTemplates().map((template) => template.id),
    }),
    computation: ({ childId, templateIds }, previous) => {
      const kept = previous?.value;
      if (
        previous?.source.childId === childId &&
        kept !== undefined &&
        (kept === NEW_TEMPLATE || kept === '' || templateIds.includes(kept))
      ) {
        return kept;
      }
      if (templateIds.length === 0) {
        return NEW_TEMPLATE;
      }
      return templateIds.length === 1 ? (templateIds[0] ?? '') : '';
    },
  });

  protected readonly selectedTemplate = computed(
    () => this.childTemplates().find((template) => template.id === this.templateChoice()) ?? null,
  );

  protected readonly name = signal('');
  protected readonly icon = signal(DEFAULT_ICON);
  protected readonly color = signal<string>(DEFAULT_COLOR);
  // The template this step created and how many rows it has saved, for resuming after a failure.
  private readonly createdTemplateId = signal<string | null>(null);
  protected readonly savedRows = signal(0);
  protected readonly saving = createAction();

  // Steps still needed: all of a new routine's, or what an existing one lacks.
  protected readonly stepsNeeded = computed(() => {
    const template = this.selectedTemplate();
    return Math.max(0, MIN_SUBTASKS - (template?.subtasks.length ?? 0));
  });
  // Fresh rows whenever the routine choice changes: a new routine's minimum, or what an existing
  // one lacks.
  protected readonly rows = linkedSignal<string, SubtaskRow[]>({
    source: this.templateChoice,
    computation: (choice) =>
      blankRows(choice === NEW_TEMPLATE ? MIN_SUBTASKS : untracked(this.stepsNeeded)),
  });
  protected readonly needsSubtasks = computed(
    () => this.templateChoice() === NEW_TEMPLATE || this.stepsNeeded() > 0,
  );
  protected readonly canSave = computed(() => {
    const filled = this.rows().filter((row) => row.title.trim() && row.minutes > 0).length;
    const nameOk = this.templateChoice() !== NEW_TEMPLATE || this.name().trim().length > 0;
    return nameOk && filled === this.rows().length && filled >= this.stepsNeeded();
  });

  // Preselected only when there is exactly one calendar to choose from; a choice survives reloads.
  protected readonly calendarId = linkedSignal<string[], string>({
    source: () => this.setup().calendars.map((calendar) => calendar.id),
    computation: (ids, previous) => {
      if (previous !== undefined && ids.includes(previous.value)) {
        return previous.value;
      }
      return ids.length === 1 ? (ids[0] ?? '') : '';
    },
  });
  protected readonly date = signal(todayIsoDate());
  // The guide only finds a routine again within its look-ahead (OnboardingService.loadSetup).
  protected readonly dateInRange = computed(() =>
    isWithinDaysAhead(this.date(), ROUTINE_LOOKAHEAD_DAYS),
  );
  protected readonly time = signal(DEFAULT_START_TIME);
  protected readonly scheduling = createAction();

  protected chooseTemplate(choice: string): void {
    this.templateChoice.set(choice);
    this.createdTemplateId.set(null);
    this.savedRows.set(0);
  }

  protected updateRow(index: number, changes: Partial<SubtaskRow>): void {
    this.rows.update((rows) => rows.map((row, i) => (i === index ? { ...row, ...changes } : row)));
  }

  protected addRow(): void {
    this.rows.update((rows) => [...rows, ...blankRows(1)]);
  }

  // Rows an earlier, failed attempt already saved stay as they are, so the retry adds the rest.
  protected isSaved(index: number): boolean {
    return index < this.savedRows();
  }

  protected removeRow(index: number): void {
    this.rows.update((rows) => rows.filter((_, i) => i !== index));
  }

  protected async save(): Promise<void> {
    const childId = this.childId();

    if (!this.canSave() || !childId) {
      return;
    }

    await this.saving.run(
      true,
      async () => {
        let templateId = this.selectedTemplate()?.id ?? this.createdTemplateId();

        if (templateId === null) {
          const created = await this.taskLibrary.createTaskTemplate(childId, {
            name: this.name().trim(),
            icon: this.icon().trim() || DEFAULT_ICON,
            color: this.color(),
          });
          templateId = created.id;
          this.createdTemplateId.set(templateId);
        }

        // Rows before savedRows were saved by an earlier, failed attempt.
        for (const [index, row] of this.rows().entries()) {
          if (index >= this.savedRows()) {
            await this.taskLibrary.addSubtask(templateId, row.title.trim(), null, row.minutes);
            this.savedRows.set(index + 1);
          }
        }

        this.templateChoice.set(templateId);
        this.createdTemplateId.set(null);
        this.savedRows.set(0);
        this.name.set('');
        this.changed.emit();
      },
      'onboarding.task.saveError',
    );
  }

  protected async schedule(): Promise<void> {
    const template = this.selectedTemplate();
    const calendarId = this.calendarId();

    if (
      template === null ||
      template.subtasks.length < MIN_SUBTASKS ||
      !calendarId ||
      !this.dateInRange()
    ) {
      return;
    }

    await this.scheduling.run(
      true,
      async () => {
        await this.calendars.scheduleTaskFromTemplate(calendarId, {
          taskTemplateId: template.id,
          startDate: this.date(),
          startTime: `${this.time()}:00`,
          recurrence: null,
          assignedTo: this.childId(),
          title: template.name,
          icon: template.icon,
          color: template.color,
        });
        this.changed.emit();
      },
      'onboarding.task.scheduleError',
    );
  }
}

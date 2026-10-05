import {
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  linkedSignal,
  resource,
  signal,
  untracked,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { sortByName, swapped } from '../../../../core/array-utils';
import { CalendarSummary, CalendarsService } from '../../../../core/calendars.service';
import { nextWeekdayOnOrAfter, todayIsoDate } from '../../../../core/date-utils';
import {
  ChildSummary,
  GuardianSummary,
  GuardiansService,
} from '../../../../core/guardians.service';
import { GroupSummary, GroupsService } from '../../../../core/groups.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import {
  PER_ITEM_REQUEST_CONCURRENCY,
  mapWithConcurrency,
} from '../../../../core/map-with-concurrency';
import { MealSlot } from '../../../../core/mealplans.service';
import {
  PRINT_ROW_KIND,
  PaperSize,
  PrintRowKind,
  PrintTemplate,
  PrintTemplateRow,
  PrintTemplatesService,
  emptyRow,
} from '../../../../core/print-templates.service';
import { UsersService } from '../../../../core/users.service';
import {
  Weekday,
  WorkLocation,
  WorkLocationsService,
} from '../../../../core/work-locations.service';
import { createAction } from '../../../../shared/action-state/action-state';
import { ColorSwatchPicker } from '../../../../shared/color-swatch-picker/color-swatch-picker';
import { LoadingSpinner } from '../../../../shared/loading-spinner/loading-spinner';
import { RepeatableRow } from '../../../../shared/repeatable-row/repeatable-row';
import {
  SegmentedControl,
  SegmentedControlOption,
} from '../../../../shared/segmented-control/segmented-control';
import { Stepper } from '../../../../shared/stepper/stepper';
import { Toggle } from '../../../../shared/toggle/toggle';
import { assembleWeekPlan } from '../assemble-week-plan';
import { PAPER_MM, WeekPlanSheet } from '../sheet/week-plan-sheet';
import {
  MAX_CALENDARS_PER_ROW,
  cleanRow,
  exampleRows,
  missingField,
  usesCalendars,
} from '../template-rows';
import { WeekPlanLoader } from '../week-plan-loader';
import { WeekPlanSources } from '../week-plan-model';

const MAX_ROWS = 12;
const MAX_HEIGHT = 5;
const PREVIEW_DEBOUNCE_MS = 400;
const PREVIEW_WIDTH_PX = 384;
const PX_PER_MM = 96 / 25.4;
const WEEKDAYS_MONDAY_FIRST: readonly Weekday[] = [1, 2, 3, 4, 5, 6, 0];
// Sunday 2026-10-04 + n days walks Sunday..Saturday, matching DayOfWeek ordinals.
const A_SUNDAY = new Date(2026, 9, 4);

const KIND_LABELS: Record<PrintRowKind, string> = {
  0: 'print.editor.kinds.meal',
  1: 'print.editor.kinds.pickup',
  2: 'print.editor.kinds.workLocation',
  3: 'print.editor.kinds.calendarMarker',
  4: 'print.editor.kinds.calendarEvents',
  5: 'print.editor.kinds.taskChecklist',
  6: 'print.editor.kinds.blank',
};

const MEAL_SLOT_LABELS: Record<MealSlot, string> = {
  0: 'print.editor.mealSlots.breakfast',
  1: 'print.editor.mealSlots.lunch',
  2: 'print.editor.mealSlots.dinner',
  3: 'print.editor.mealSlots.snack',
};

interface DraftRow {
  key: number;
  row: PrintTemplateRow;
}

// What the editor loads: the template plus everything its rows can refer to.
interface LoadedEditor {
  template: PrintTemplate;
  children: ChildSummary[];
  calendars: CalendarSummary[];
  groups: GroupSummary[];
  guardians: GuardianSummary[];
  workLocations: ReadonlyMap<string, WorkLocation[]>;
}

const EMPTY_SOURCES: WeekPlanSources = {
  meals: new Map(),
  pickups: new Map(),
  workDays: new Map(),
  occurrences: new Map(),
  names: new Map(),
};

// Edits one template as a draft. Nothing is sent until Save, which sends only the parts that
// changed (name, layout, rows, colors) and then replaces the draft with the server's response --
// no optimistic update, the same stance as the pickup planner.
@Component({
  selector: 'app-print-template-editor',
  imports: [
    FormsModule,
    RouterLink,
    TranslatePipe,
    ColorSwatchPicker,
    LoadingSpinner,
    RepeatableRow,
    SegmentedControl,
    Stepper,
    Toggle,
    WeekPlanSheet,
  ],
  templateUrl: './print-template-editor.html',
})
export class PrintTemplateEditor {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly templates = inject(PrintTemplatesService);
  private readonly guardiansService = inject(GuardiansService);
  private readonly calendarsService = inject(CalendarsService);
  private readonly groupsService = inject(GroupsService);
  private readonly workLocationsService = inject(WorkLocationsService);
  private readonly users = inject(UsersService);
  private readonly loader = inject(WeekPlanLoader);
  private readonly translation = inject(TranslationService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly kind = PRINT_ROW_KIND;
  protected readonly kindLabels = KIND_LABELS;
  protected readonly kinds = Object.keys(KIND_LABELS).map(Number) as PrintRowKind[];
  protected readonly mealSlotLabels = MEAL_SLOT_LABELS;
  protected readonly mealSlots = [0, 1, 2, 3] as MealSlot[];
  protected readonly maxItemsOptions = [1, 2, 3, 4, 5, 6, 7, 8];
  protected readonly maxRows = MAX_ROWS;
  protected readonly maxHeight = MAX_HEIGHT;
  protected readonly weekdays = WEEKDAYS_MONDAY_FIRST;
  protected readonly usesCalendars = usesCalendars;

  private readonly templateId = this.route.snapshot.paramMap.get('templateId') ?? '';
  private nextKey = 0;

  // The template plus everything its rows can refer to.
  protected readonly editor = resource({ loader: () => this.load() });
  // The template as last loaded or saved; the draft below resets from it.
  protected readonly template = computed(() =>
    this.editor.hasValue() ? this.editor.value().template : undefined,
  );
  // Shared by Save and Delete: either one disables both, and their errors show in one place.
  protected readonly saving = createAction();
  protected readonly confirmingDelete = signal(false);
  private readonly justSaved = signal(false);
  // The draft as last saved -- "Template saved." only shows while the draft still matches it.
  private readonly savedDraft = signal('');
  protected readonly savedStatus = computed(
    () => this.justSaved() && JSON.stringify(this.draftTemplate()) === this.savedDraft(),
  );

  protected readonly children = computed(() =>
    this.editor.hasValue() ? this.editor.value().children : [],
  );
  protected readonly guardians = computed(() =>
    this.editor.hasValue() ? this.editor.value().guardians : [],
  );
  protected readonly calendars = computed(() =>
    this.editor.hasValue() ? this.editor.value().calendars : [],
  );
  protected readonly groups = computed(() =>
    this.editor.hasValue() ? this.editor.value().groups : [],
  );
  protected readonly workLocations = computed((): ReadonlyMap<string, WorkLocation[]> =>
    this.editor.hasValue() ? this.editor.value().workLocations : new Map<string, WorkLocation[]>(),
  );

  protected readonly name = linkedSignal(() => this.template()?.name ?? '');
  protected readonly paperSize = linkedSignal((): PaperSize => this.template()?.paperSize ?? 0);
  protected readonly startWeekday = linkedSignal(
    (): Weekday => this.template()?.defaultStartWeekday ?? 1,
  );
  protected readonly showWeekNumber = linkedSignal(() => this.template()?.showWeekNumber ?? true);
  protected readonly rows = linkedSignal(() =>
    (this.template()?.rows ?? []).map((row) => this.draft(row)),
  );
  protected readonly colors = linkedSignal((): Partial<Record<string, string>> =>
    Object.fromEntries((this.template()?.guardianColors ?? []).map((c) => [c.guardianId, c.color])),
  );
  protected readonly newKind = signal<PrintRowKind>(PRINT_ROW_KIND.blank);

  private readonly previewSources = signal<WeekPlanSources>(EMPTY_SOURCES);

  protected readonly paperOptions = computed((): SegmentedControlOption<PaperSize>[] => {
    this.translation.language();
    return [
      { value: 0, label: this.translation.translate('print.editor.paperA4') },
      { value: 1, label: this.translation.translate('print.editor.paperA3') },
    ];
  });

  protected readonly weekdayNames = computed(() => {
    const locale = this.translation.language();
    return new Map(
      WEEKDAYS_MONDAY_FIRST.map((day) => {
        const date = new Date(
          A_SUNDAY.getFullYear(),
          A_SUNDAY.getMonth(),
          A_SUNDAY.getDate() + day,
        );
        return [day, date.toLocaleDateString(locale, { weekday: 'long' })];
      }),
    );
  });

  private readonly childIds = computed(() => new Set(this.children().map((c) => c.id)));
  private readonly calendarIds = computed(() => new Set(this.calendars().map((c) => c.id)));
  private readonly guardianIds = computed(() => new Set(this.guardians().map((g) => g.id)));
  private readonly groupIds = computed(() => new Set(this.groups().map((g) => g.id)));

  protected readonly draftTemplate = computed((): PrintTemplate | null => {
    const template = this.template();
    if (!template) {
      return null;
    }

    return {
      ...template,
      name: this.name(),
      paperSize: this.paperSize(),
      defaultStartWeekday: this.startWeekday(),
      showWeekNumber: this.showWeekNumber(),
      rows: this.rows().map((draft) => cleanRow(draft.row)),
      guardianColors: Object.entries(this.colors()).flatMap(([guardianId, color]) =>
        color === undefined ? [] : [{ guardianId, color }],
      ),
    };
  });

  protected readonly firstProblem = computed(() => {
    if (!this.name().trim()) {
      return 'print.editor.missing.name';
    }
    if (this.rows().length === 0) {
      return 'print.editor.missing.rows';
    }
    for (const draft of this.rows()) {
      const missing = missingField(draft.row);
      if (missing) {
        return missing;
      }
    }
    return null;
  });

  protected readonly previewStart = computed(() =>
    nextWeekdayOnOrAfter(todayIsoDate(), this.startWeekday()),
  );

  protected readonly previewModel = computed(() => {
    const template = this.draftTemplate();
    return template
      ? assembleWeekPlan(template, this.previewSources(), {
          start: this.previewStart(),
          locale: this.translation.language(),
          timeZone: this.users.timeZoneId(),
          labels: {
            week: this.translation.translate('print.sheet.week'),
            selfEscort: this.translation.translate('print.sheet.selfEscort'),
            playdate: this.translation.translate('print.sheet.playdate'),
            babysitter: this.translation.translate('print.sheet.babysitter'),
          },
          includeSubtasks: false,
        })
      : null;
  });

  // A4 and A3 both scale into the same preview width, keeping the paper's proportions.
  protected readonly previewBox = computed(() => {
    const paper = PAPER_MM[this.paperSize()];
    const scale = PREVIEW_WIDTH_PX / (paper.width * PX_PER_MM);
    return { scale, width: PREVIEW_WIDTH_PX, height: paper.height * PX_PER_MM * scale };
  });

  // Only refetch preview data when the set of referenced sources changes, not on every keystroke.
  private readonly previewSourceKey = computed(() => {
    const template = this.draftTemplate();
    return template
      ? JSON.stringify([
          this.previewStart(),
          template.rows.map((r) => [r.kind, r.childId, r.mealGroupId, r.guardianId, r.calendarIds]),
        ])
      : '';
  });

  private previewTimer: ReturnType<typeof setTimeout> | null = null;
  private latestPreview = 0;

  constructor() {
    this.destroyRef.onDestroy(() => {
      if (this.previewTimer) {
        clearTimeout(this.previewTimer);
      }
    });

    effect(() => {
      const key = this.previewSourceKey();
      if (!key) {
        return;
      }
      untracked(() => this.schedulePreview());
    });
  }

  protected rowIsStale(row: PrintTemplateRow): boolean {
    const location = row.workLocationId
      ? this.workLocations()
          .get(row.guardianId ?? '')
          ?.find((l) => l.id === row.workLocationId)
      : undefined;

    return (
      (!!row.childId && !this.childIds().has(row.childId)) ||
      (!!row.mealGroupId && !this.groupIds().has(row.mealGroupId)) ||
      (!!row.guardianId && !this.guardianIds().has(row.guardianId)) ||
      (row.calendarIds ?? []).some((id) => !this.calendarIds().has(id)) ||
      (!!row.workLocationId && (!location || location.isArchived))
    );
  }

  // Calendars a row names that this guardian can't see (another member's private calendar, a
  // deleted one) -- listed so they can be unticked rather than only removed with the whole row.
  protected unknownCalendars(row: PrintTemplateRow): string[] {
    return (row.calendarIds ?? []).filter((id) => !this.calendarIds().has(id));
  }

  protected calendarLimitReached(row: PrintTemplateRow, calendarId: string): boolean {
    return (
      !this.hasCalendar(row, calendarId) && (row.calendarIds?.length ?? 0) >= MAX_CALENDARS_PER_ROW
    );
  }

  protected rowProblem(row: PrintTemplateRow): string | null {
    return missingField(row);
  }

  protected locationsFor(row: PrintTemplateRow): WorkLocation[] {
    const all = this.workLocations().get(row.guardianId ?? '') ?? [];
    return all.filter((l) => !l.isArchived || l.id === row.workLocationId);
  }

  protected guardianName(guardian: GuardianSummary): string {
    return `${guardian.name.givenName} ${guardian.name.familyName}`.trim();
  }

  protected mealScope(row: PrintTemplateRow): string {
    return row.mealGroupId ? `group:${row.mealGroupId}` : row.childId ? `child:${row.childId}` : '';
  }

  protected setMealScope(key: number, scope: string): void {
    const [type, id] = scope.split(':');
    this.update(key, {
      childId: type === 'child' ? id : null,
      mealGroupId: type === 'group' ? id : null,
    });
  }

  protected hasCalendar(row: PrintTemplateRow, calendarId: string): boolean {
    return (row.calendarIds ?? []).includes(calendarId);
  }

  protected toggleCalendar(
    key: number,
    row: PrintTemplateRow,
    calendarId: string,
    checked: boolean,
  ): void {
    const current = row.calendarIds ?? [];
    const next = checked
      ? [...current.filter((id) => id !== calendarId), calendarId]
      : current.filter((id) => id !== calendarId);
    this.update(key, { calendarIds: next });
  }

  protected update(key: number, changes: Partial<PrintTemplateRow>): void {
    this.rows.update((rows) =>
      rows.map((draft) => (draft.key === key ? { key, row: { ...draft.row, ...changes } } : draft)),
    );
  }

  protected changeKind(key: number, kind: PrintRowKind): void {
    // Switching kind starts from a clean row (keeping label and height) so no stray fields linger.
    this.rows.update((rows) =>
      rows.map((draft) =>
        draft.key === key
          ? {
              key,
              row: { ...emptyRow(kind, draft.row.label), heightWeight: draft.row.heightWeight },
            }
          : draft,
      ),
    );
  }

  protected addRow(): void {
    this.rows.update((rows) => [...rows, this.draft(emptyRow(this.newKind(), ''))]);
  }

  protected removeRow(key: number): void {
    this.rows.update((rows) => rows.filter((draft) => draft.key !== key));
  }

  protected move(index: number, offset: number): void {
    this.rows.update((rows) => {
      const target = index + offset;
      if (target < 0 || target >= rows.length) {
        return rows;
      }
      return swapped(rows, index, target);
    });
  }

  protected setColor(guardianId: string, color: string | null): void {
    this.colors.update((colors) => {
      const next = { ...colors };
      if (color) {
        next[guardianId] = color;
      } else {
        delete next[guardianId];
      }
      return next;
    });
  }

  protected fillExample(): void {
    const t = (key: string, params?: Record<string, string>) =>
      this.translation.translate(key, params);
    const rows = exampleRows({
      children: this.children(),
      guardians: this.guardians(),
      calendars: this.calendars(),
      workLocations: this.workLocations(),
      labels: {
        dinner: t('print.editor.example.dinner'),
        pickup: (child) => t('print.editor.example.pickup', { child }),
        atLocation: (guardian, location) =>
          t('print.editor.example.atLocation', { guardian, location }),
        activities: (child) => t('print.editor.example.activities', { child }),
        chores: (child) => t('print.editor.example.chores', { child }),
        appointments: t('print.editor.example.appointments'),
        notes: t('print.editor.example.notes'),
      },
    });
    this.rows.set(rows.map((row) => this.draft(row)));
  }

  protected async save(): Promise<void> {
    const template = this.template();
    const draft = this.draftTemplate();
    if (!template || !draft) {
      return;
    }

    await this.saving.run(
      true,
      async () => {
        let saved = template;
        if (draft.name.trim() !== template.name) {
          saved = await this.templates.rename(template.id, draft.name.trim());
        }
        if (
          draft.paperSize !== template.paperSize ||
          draft.defaultStartWeekday !== template.defaultStartWeekday ||
          draft.showWeekNumber !== template.showWeekNumber
        ) {
          saved = await this.templates.updateLayout(template.id, {
            paperSize: draft.paperSize,
            defaultStartWeekday: draft.defaultStartWeekday,
            showWeekNumber: draft.showWeekNumber,
          });
        }
        if (JSON.stringify(draft.rows) !== JSON.stringify(template.rows)) {
          saved = await this.templates.replaceRows(template.id, draft.rows);
        }
        if (JSON.stringify(draft.guardianColors) !== JSON.stringify(template.guardianColors)) {
          saved = await this.templates.replaceColors(template.id, draft.guardianColors);
        }

        // Resets the draft to the server's response.
        this.editor.update((current) => current && { ...current, template: saved });
        this.savedDraft.set(JSON.stringify(this.draftTemplate()));
        this.justSaved.set(true);
      },
      'print.editor.saveError',
    );
  }

  protected async remove(): Promise<void> {
    await this.saving.run(
      true,
      async () => {
        await this.templates.delete(this.templateId);
        await this.router.navigate(['/guardian/print']);
      },
      'print.editor.deleteError',
    );
  }

  private draft(row: PrintTemplateRow): DraftRow {
    return { key: this.nextKey++, row };
  }

  private schedulePreview(): void {
    if (this.previewTimer) {
      clearTimeout(this.previewTimer);
    }
    this.previewTimer = setTimeout(() => void this.loadPreview(), PREVIEW_DEBOUNCE_MS);
  }

  private async loadPreview(): Promise<void> {
    const template = this.draftTemplate();
    if (!template) {
      return;
    }

    const load = ++this.latestPreview;
    const sources = await this.loader.load(template, this.previewStart());
    if (load === this.latestPreview) {
      this.previewSources.set(sources);
    }
  }

  private async load(): Promise<LoadedEditor> {
    const [template, children, calendars, groups, me] = await Promise.all([
      this.templates.get(this.templateId),
      this.guardiansService.listMyChildren(),
      this.calendarsService.listMyCalendars().then(sortByName),
      this.groupsService.listMyGroups(),
      this.users.ensureCurrentUser(),
    ]);

    // The guardian themself plus every guardian of their children -- the people a work-location
    // row or a name color can refer to. Themself first, and even without children linked.
    const lists = await mapWithConcurrency(children, PER_ITEM_REQUEST_CONCURRENCY, (child) =>
      this.guardiansService.listChildGuardians(child.id).catch(() => [] as GuardianSummary[]),
    );
    const self: GuardianSummary = { id: me.id, name: me.name, guardianLinkId: '', kind: 0 };
    const guardians = [...new Map([self, ...lists.flat()].map((g) => [g.id, g])).values()];

    const schedules = await mapWithConcurrency(
      guardians,
      PER_ITEM_REQUEST_CONCURRENCY,
      (guardian) =>
        this.workLocationsService
          .getSchedule(guardian.id)
          .then((s) => [guardian.id, s.locations] as const)
          .catch(() => [guardian.id, [] as WorkLocation[]] as const),
    );

    return {
      template,
      children,
      calendars,
      groups,
      guardians,
      workLocations: new Map(schedules),
    };
  }
}

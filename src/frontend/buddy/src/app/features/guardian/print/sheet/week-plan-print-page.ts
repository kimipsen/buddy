import {
  Component,
  DestroyRef,
  DOCUMENT,
  ElementRef,
  HostListener,
  Injector,
  afterNextRender,
  computed,
  effect,
  inject,
  linkedSignal,
  resource,
  signal,
  viewChild,
} from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { nextWeekdayOnOrAfter, todayIsoDate } from '../../../../core/date-utils';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { PrintTemplate, PrintTemplatesService } from '../../../../core/print-templates.service';
import { UsersService } from '../../../../core/users.service';
import { DateSelect } from '../../../../shared/date-select/date-select';
import { LoadingSpinner } from '../../../../shared/loading-spinner/loading-spinner';
import { Toggle } from '../../../../shared/toggle/toggle';
import { assembleWeekPlan } from '../assemble-week-plan';
import { WeekPlanLoader } from '../week-plan-loader';
import { WeekPlanModel } from '../week-plan-model';
import { PAPER_MM, WeekPlanSheet } from './week-plan-sheet';

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;
const PX_PER_MM = 96 / 25.4;
const PREVIEW_GUTTER_PX = 32;

// The printable route, deliberately outside GuardianShell so no navigation ends up on paper.
// It owns the one global <style> carrying @page -- @page can't be scoped to a component (it has no
// selector for emulated encapsulation to rewrite) -- adding it once the template arrives and
// removing it on destroy.
@Component({
  selector: 'app-week-plan-print-page',
  imports: [RouterLink, TranslatePipe, DateSelect, LoadingSpinner, Toggle, WeekPlanSheet],
  templateUrl: './week-plan-print-page.html',
})
export class WeekPlanPrintPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly document = inject(DOCUMENT);
  private readonly destroyRef = inject(DestroyRef);
  private readonly templates = inject(PrintTemplatesService);
  private readonly loader = inject(WeekPlanLoader);
  private readonly users = inject(UsersService);
  private readonly translation = inject(TranslationService);
  private readonly injector = inject(Injector);

  private readonly printButton = viewChild<ElementRef<HTMLButtonElement>>('printButton');

  protected readonly templateId = this.route.snapshot.paramMap.get('templateId') ?? '';
  protected readonly template = resource({
    loader: async ({ abortSignal }) => {
      const [template] = await Promise.all([
        this.templates.get(this.templateId),
        this.users.ensureCurrentUser(),
      ]);
      // Left before the template arrived (destroying the page aborts the load): adding the style
      // now would outlive the page and set the paper size for every later print in this tab.
      if (!abortSignal.aborted) {
        this.applyPageStyle(template);
      }
      return template;
    },
  });

  // A missing or malformed start falls back to the next default start weekday.
  protected readonly start = linkedSignal(() => {
    if (!this.template.hasValue()) {
      return '';
    }
    const requested = this.route.snapshot.queryParamMap.get('start') ?? '';
    return ISO_DATE.test(requested)
      ? requested
      : nextWeekdayOnOrAfter(todayIsoDate(), this.template.value().defaultStartWeekday);
  });

  // Print a routine's subtasks under its parent title; kept in the URL like the start date.
  protected readonly includeSubtasks = signal(
    this.route.snapshot.queryParamMap.get('subtasks') === '1',
  );

  // Refetches whenever the date changes; a newer date picked while one loads wins.
  protected readonly sheet = resource({
    params: () =>
      this.template.hasValue() && this.start()
        ? { template: this.template.value(), start: this.start() }
        : undefined,
    loader: async ({ params: { template, start } }) => ({
      template,
      start,
      sources: await this.loader.load(template, start),
    }),
  });

  // The sheet on screen: the previous date's stays up while a new date loads. Toggling subtasks
  // only reassembles what's already fetched.
  protected readonly model = linkedSignal<WeekPlanModel | undefined, WeekPlanModel | undefined>({
    source: () => {
      if (!this.sheet.hasValue()) {
        return undefined;
      }
      const { template, start, sources } = this.sheet.value();
      return assembleWeekPlan(template, sources, {
        start,
        locale: this.translation.language(),
        timeZone: this.users.timeZoneId(),
        labels: {
          week: this.translation.translate('print.sheet.week'),
          selfEscort: this.translation.translate('print.sheet.selfEscort'),
          playdate: this.translation.translate('print.sheet.playdate'),
          babysitter: this.translation.translate('print.sheet.babysitter'),
        },
        includeSubtasks: this.includeSubtasks(),
      });
    },
    computation: (model, previous) => model ?? previous?.value,
  });

  protected readonly loadFailed = computed(() => !!this.template.error() || !!this.sheet.error());

  private readonly viewportWidth = signal(this.document.defaultView?.innerWidth ?? 1280);
  private pageStyle: HTMLStyleElement | null = null;
  private focusedOnce = false;

  // On screen the sheet keeps its millimetre size and is scaled down to fit, so the preview is a
  // faithful miniature of the paper. The scale is dropped under @media print.
  protected readonly previewScale = computed(() => {
    const paper = PAPER_MM[this.model()?.paperSize ?? 0];
    return Math.min(1, (this.viewportWidth() - PREVIEW_GUTTER_PX) / (paper.width * PX_PER_MM));
  });

  protected readonly previewSize = computed(() => {
    const paper = PAPER_MM[this.model()?.paperSize ?? 0];
    const scale = this.previewScale();
    return { width: paper.width * PX_PER_MM * scale, height: paper.height * PX_PER_MM * scale };
  });

  @HostListener('window:resize')
  protected onResize(): void {
    this.viewportWidth.set(this.document.defaultView?.innerWidth ?? this.viewportWidth());
  }

  constructor() {
    this.destroyRef.onDestroy(() => this.pageStyle?.remove());

    // Print is the default action on the preview: focus it once, when the sheet first appears --
    // not again on every date change, which would pull focus out of the date input mid-typing.
    effect(() => {
      if (this.model() && !this.focusedOnce) {
        this.focusedOnce = true;
        afterNextRender(() => this.printButton()?.nativeElement.focus(), {
          injector: this.injector,
        });
      }
    });
  }

  protected print(): void {
    this.document.defaultView?.print();
  }

  protected changeDate(date: string): void {
    if (!ISO_DATE.test(date)) {
      return;
    }
    void this.router.navigate([], {
      queryParams: { start: date },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
    this.start.set(date);
  }

  protected setIncludeSubtasks(include: boolean): void {
    void this.router.navigate([], {
      queryParams: { subtasks: include ? '1' : null },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
    this.includeSubtasks.set(include);
  }

  private applyPageStyle(template: PrintTemplate): void {
    const size = template.paperSize === 1 ? 'A3' : 'A4';
    this.pageStyle ??= this.document.head.appendChild(this.document.createElement('style'));
    this.pageStyle.setAttribute('data-week-plan-print', '');
    // Paper is always white, whatever the app theme: the dark class on <html> must not print.
    this.pageStyle.textContent =
      `@page { size: ${size} landscape; margin: 8mm; }\n` +
      '@media print { html, body { background: #fff !important; color-scheme: light; } }';
  }
}

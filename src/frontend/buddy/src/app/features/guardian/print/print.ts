import { Component, computed, inject, linkedSignal, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

import { nextWeekdayOnOrAfter, todayIsoDate } from '../../../core/date-utils';
import { GroupSummary, GroupsService } from '../../../core/groups.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { PrintTemplate, PrintTemplatesService } from '../../../core/print-templates.service';
import { createAction } from '../../../shared/action-state/action-state';
import { DateSelect } from '../../../shared/date-select/date-select';
import { LoadingSpinner } from '../../../shared/loading-spinner/loading-spinner';
import { readLastTemplateId, writeLastTemplateId } from './last-template-storage';
import { Card } from '../../../shared/card/card';
import { Page } from '../../../shared/page/page';

const PERSONAL = '';

// The everyday path: pick a template and a start date, preview, print. Also where a new template
// is created; building it happens in the editor.
@Component({
  selector: 'app-guardian-print',
  imports: [FormsModule, RouterLink, TranslatePipe, DateSelect, LoadingSpinner, Card, Page],
  templateUrl: './print.html',
})
export class GuardianPrint {
  private readonly templates = inject(PrintTemplatesService);
  private readonly groups = inject(GroupsService);
  private readonly router = inject(Router);

  protected readonly personal = PERSONAL;

  // The templates to choose from and the groups a new one can belong to. Groups are best-effort:
  // without them the owner picker just offers "Just me".
  protected readonly page = resource({
    loader: async () => {
      const [list, groups] = await Promise.all([
        this.templates.list(),
        this.groups.listMyGroups().catch(() => [] as GroupSummary[]),
      ]);
      return { list, groups };
    },
  });
  protected readonly list = computed(() => (this.page.hasValue() ? this.page.value().list : []));
  protected readonly groupList = computed(() =>
    this.page.hasValue() ? this.page.value().groups : [],
  );

  // What the dropdown shows: the remembered template, else the first. Preview/Edit use `selected`,
  // which has no value while a new choice loads, so they can never act on a different template
  // than the one displayed.
  protected readonly selectedId = linkedSignal(() => {
    const list = this.list();
    const remembered = readLastTemplateId();
    return (list.find((t) => t.id === remembered) ?? list[0])?.id ?? '';
  });
  protected readonly selected = resource({
    params: () => {
      const templateId = this.selectedId();
      return templateId ? { templateId } : undefined;
    },
    loader: async ({ params, abortSignal }) => {
      const template = await this.templates.get(params.templateId);
      if (!abortSignal.aborted) {
        writeLastTemplateId(params.templateId);
      }
      return template;
    },
  });
  private readonly selectedTemplate = computed((): PrintTemplate | undefined =>
    this.selected.hasValue() ? this.selected.value() : undefined,
  );
  // Pre-set to the selected template's default start weekday on or after today; kept as is while a
  // new choice loads.
  protected readonly start = linkedSignal<PrintTemplate | undefined, string>({
    source: this.selectedTemplate,
    computation: (template, previous) =>
      template
        ? nextWeekdayOnOrAfter(todayIsoDate(), template.defaultStartWeekday)
        : (previous?.value ?? todayIsoDate()),
  });
  protected readonly loadFailed = computed(() => !!this.page.error() || !!this.selected.error());

  protected readonly newName = signal('');
  protected readonly newOwner = signal(PERSONAL);
  protected readonly creating = createAction();

  protected readonly groupNames = computed(
    () => new Map(this.groupList().map((g) => [g.id, g.name])),
  );

  protected select(templateId: string): void {
    this.selectedId.set(templateId);
    this.creating.clearError();
  }

  protected preview(): void {
    const template = this.selectedTemplate();
    if (template) {
      void this.router.navigate(['/guardian/print/sheet', template.id], {
        queryParams: { start: this.start() },
      });
    }
  }

  protected async create(): Promise<void> {
    await this.creating.run(
      true,
      async () => {
        const created = await this.templates.create(this.newName().trim(), this.newOwner() || null);
        writeLastTemplateId(created.id);
        await this.router.navigate(['/guardian/print/templates', created.id]);
      },
      'print.list.createError',
    );
  }
}

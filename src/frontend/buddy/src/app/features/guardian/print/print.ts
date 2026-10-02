import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

import { nextWeekdayOnOrAfter, todayIsoDate } from '../../../core/date-utils';
import { GroupSummary, GroupsService } from '../../../core/groups.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import {
  PrintTemplate,
  PrintTemplateSummary,
  PrintTemplatesService,
} from '../../../core/print-templates.service';
import { DateSelect } from '../../../shared/date-select/date-select';
import { LoadingSpinner } from '../../../shared/loading-spinner/loading-spinner';
import { readLastTemplateId, writeLastTemplateId } from './last-template-storage';

const PERSONAL = '';

// The everyday path: pick a template and a start date, preview, print. Also where a new template
// is created; building it happens in the editor.
@Component({
  selector: 'app-guardian-print',
  imports: [FormsModule, RouterLink, TranslatePipe, DateSelect, LoadingSpinner],
  templateUrl: './print.html',
})
export class GuardianPrint implements OnInit {
  private readonly templates = inject(PrintTemplatesService);
  private readonly groups = inject(GroupsService);
  private readonly router = inject(Router);

  protected readonly personal = PERSONAL;
  protected readonly list = signal<PrintTemplateSummary[]>([]);
  protected readonly groupList = signal<GroupSummary[]>([]);
  protected readonly selected = signal<PrintTemplate | null>(null);
  // What the dropdown shows. Preview/Edit use `selected`, which is cleared while a new choice
  // loads, so they can never act on a different template than the one displayed.
  protected readonly selectedId = signal('');
  private latestSelect = 0;
  protected readonly start = signal(todayIsoDate());
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly newName = signal('');
  protected readonly newOwner = signal(PERSONAL);
  protected readonly creating = signal(false);

  protected readonly groupNames = computed(
    () => new Map(this.groupList().map((g) => [g.id, g.name])),
  );

  ngOnInit(): void {
    void this.load();
  }

  protected async select(templateId: string): Promise<void> {
    this.error.set(null);
    this.selectedId.set(templateId);
    this.selected.set(null);
    const request = ++this.latestSelect;

    try {
      const template = await this.templates.get(templateId);
      if (request !== this.latestSelect) {
        return;
      }
      this.selected.set(template);
      // Pre-set to the template's default start weekday on or after today.
      this.start.set(nextWeekdayOnOrAfter(todayIsoDate(), template.defaultStartWeekday));
      writeLastTemplateId(templateId);
    } catch {
      if (request === this.latestSelect) {
        this.error.set('print.list.loadError');
      }
    }
  }

  protected preview(): void {
    const template = this.selected();
    if (template) {
      void this.router.navigate(['/guardian/print/sheet', template.id], {
        queryParams: { start: this.start() },
      });
    }
  }

  protected async create(): Promise<void> {
    this.creating.set(true);
    this.error.set(null);

    try {
      const created = await this.templates.create(this.newName().trim(), this.newOwner() || null);
      writeLastTemplateId(created.id);
      await this.router.navigate(['/guardian/print/templates', created.id]);
    } catch {
      this.error.set('print.list.createError');
    } finally {
      this.creating.set(false);
    }
  }

  private async load(): Promise<void> {
    try {
      const [list, groups] = await Promise.all([
        this.templates.list(),
        this.groups.listMyGroups().catch(() => [] as GroupSummary[]),
      ]);
      this.list.set(list);
      this.groupList.set(groups);

      const remembered = readLastTemplateId();
      const initial = list.find((t) => t.id === remembered) ?? list[0];
      if (initial) {
        await this.select(initial.id);
      }
    } catch {
      this.error.set('print.list.loadError');
    } finally {
      this.loading.set(false);
    }
  }
}

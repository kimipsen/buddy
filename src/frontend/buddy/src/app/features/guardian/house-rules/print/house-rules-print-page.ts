import { Component, DOCUMENT, inject, resource } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { GroupsService } from '../../../../core/groups.service';
import { HouseRulesService } from '../../../../core/house-rules.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { LoadingSpinner } from '../../../../shared/loading-spinner/loading-spinner';
import { MarkdownView } from '../../../../shared/markdown-view/markdown-view';

export type PrintScope = 'child' | 'group';

interface PrintedRule {
  id: string;
  title: string;
  body: string;
}

interface PrintedSection {
  heading: string;
  rules: PrintedRule[];
}

export interface PrintedRules {
  title: string;
  sections: PrintedSection[];
}

// A portrait A4 printout of house rules for the fridge, outside GuardianShell like the week-plan
// sheet so no navigation ends up on paper (house-rules.md, Question 7). The child printout is
// "Emil's rules": personal rules first, then each household; the group printout is one household.
@Component({
  selector: 'app-house-rules-print-page',
  imports: [LoadingSpinner, MarkdownView, RouterLink, TranslatePipe],
  templateUrl: './house-rules-print-page.html',
})
export class HouseRulesPrintPage {
  private readonly route = inject(ActivatedRoute);
  private readonly document = inject(DOCUMENT);
  private readonly houseRules = inject(HouseRulesService);
  private readonly groups = inject(GroupsService);
  private readonly translation = inject(TranslationService);

  private readonly scope: PrintScope = this.route.snapshot.data['printScope'] as PrintScope;
  private readonly id =
    this.route.snapshot.paramMap.get(this.scope === 'child' ? 'childId' : 'groupId') ?? '';

  protected readonly printed = resource({
    loader: (): Promise<PrintedRules> =>
      this.scope === 'child' ? this.loadChild() : this.loadGroup(),
  });

  protected print(): void {
    this.document.defaultView?.print();
  }

  private async loadChild(): Promise<PrintedRules> {
    const rules = await this.houseRules.getChildRules(this.id);
    const name = rules.personal.label;

    return {
      title: this.translation.translate('houseRules.print.childTitle', { name }),
      sections: withRules([
        {
          heading: this.translation.translate('houseRules.print.personalHeading', { name }),
          rules: rules.personal.rules,
        },
        ...rules.households.map((household) => ({
          heading: household.label,
          rules: household.rules,
        })),
      ]),
    };
  }

  private async loadGroup(): Promise<PrintedRules> {
    const [book, group] = await Promise.all([
      this.houseRules.listRules({ kind: 'Group', id: this.id }),
      this.groups.getGroup(this.id),
    ]);

    return { title: group.name, sections: withRules([{ heading: '', rules: book.rules }]) };
  }
}

// Empty scopes stay off paper; the sheet says "No rules yet." once when nothing is left.
function withRules(sections: PrintedSection[]): PrintedSection[] {
  return sections.filter((section) => section.rules.length > 0);
}

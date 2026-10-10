import { Component, computed, inject, resource, signal } from '@angular/core';

import {
  ChildRule,
  ChildRuleSection,
  ChildRules as ChildRulesData,
  HouseRulesService,
  isRevisionChanged,
  scopeOf,
} from '../../../core/house-rules.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { UsersService } from '../../../core/users.service';
import { createAction } from '../../../shared/action-state/action-state';
import { LoadingSpinner } from '../../../shared/loading-spinner/loading-spinner';
import { MarkdownView } from '../../../shared/markdown-view/markdown-view';
import { ChildPage } from '../child-page/child-page';

// The child's own rules: their personal rules, then each household's, with a "New"/"Changed" chip
// and an "I've read this" button on every rule they haven't read in its current version
// (docs/backend/analysis/house-rules.md, Question 7).
@Component({
  selector: 'app-child-rules',
  imports: [ChildPage, LoadingSpinner, MarkdownView, TranslatePipe],
  templateUrl: './child-rules.html',
})
export class ChildRules {
  private readonly users = inject(UsersService);
  private readonly houseRules = inject(HouseRulesService);

  protected readonly rules = resource({
    loader: async () => this.houseRules.getChildRules((await this.users.ensureCurrentUser()).id),
  });
  protected readonly sections = computed((): ChildRuleSection[] => {
    if (!this.rules.hasValue()) {
      return [];
    }

    const { personal, households } = this.rules.value();
    return [personal, ...households].filter((section) => section.rules.length > 0);
  });

  protected readonly reading = createAction<string>();
  // The rule whose acknowledgement came back as changed, after the reload shows the new text.
  protected readonly changedRuleId = signal<string | null>(null);

  protected async markRead(section: ChildRuleSection, rule: ChildRule): Promise<void> {
    this.changedRuleId.set(null);

    await this.reading.run(
      rule.id,
      async () => {
        try {
          await this.houseRules.acknowledge(scopeOf(section), rule.id, rule.revision);
        } catch (error: unknown) {
          if (!isRevisionChanged(error)) {
            throw error;
          }

          this.changedRuleId.set(rule.id);
          this.rules.reload();
          return;
        }

        this.updateRules((current) => markedRead(current, rule));
      },
      'houseRules.child.acknowledgeError',
    );
  }

  // Only once loaded: setting the resource mid-load would cancel the load.
  private updateRules(change: (rules: ChildRulesData) => ChildRulesData): void {
    if (this.rules.hasValue() && !this.rules.isLoading()) {
      this.rules.set(change(this.rules.value()));
    }
  }
}

function markedRead(rules: ChildRulesData, read: ChildRule): ChildRulesData {
  const mark = (section: ChildRuleSection): ChildRuleSection => ({
    ...section,
    rules: section.rules.map((rule) =>
      rule.id === read.id
        ? { ...rule, isUpToDate: true, acknowledgedRevision: read.revision }
        : rule,
    ),
  });

  return {
    ...rules,
    personal: mark(rules.personal),
    households: rules.households.map(mark),
    pendingAcknowledgements: Math.max(0, rules.pendingAcknowledgements - 1),
  };
}

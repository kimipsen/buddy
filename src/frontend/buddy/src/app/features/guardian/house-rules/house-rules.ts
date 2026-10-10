import {
  Component,
  Injector,
  afterNextRender,
  computed,
  inject,
  linkedSignal,
  resource,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { GroupsService } from '../../../core/groups.service';
import { ChildSummary, GuardiansService } from '../../../core/guardians.service';
import {
  HouseRulesService,
  Rule,
  RuleAcknowledgement,
  RuleBook,
  RuleScope,
} from '../../../core/house-rules.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';
import { createAction } from '../../../shared/action-state/action-state';
import { MarkdownView } from '../../../shared/markdown-view/markdown-view';
import { Page } from '../../../shared/page/page';
import { RuleEditor, RuleEditorResult } from './rule-editor/rule-editor';

interface ScopeOption extends RuleScope {
  key: string;
  label: string;
}

type AcknowledgementStatus = 'upToDate' | 'new' | 'changed';

const STATUS_KEYS: Record<AcknowledgementStatus, string> = {
  upToDate: 'houseRules.status.upToDate',
  new: 'houseRules.status.new',
  changed: 'houseRules.status.changed',
};

// Which rule is open in the editor: a new one, or an existing one by id.
type Editing = { kind: 'new' } | { kind: 'edit'; rule: Rule } | null;

function keyOf(scope: RuleScope): string {
  return `${scope.kind}:${scope.id}`;
}

// The guardian's house rules: pick a household group or a child, read the rules with every
// child's "read it" status, and -- with Manage -- add, edit, remove and reorder them, or go
// through a rule with a child who can't read yet (docs/backend/analysis/house-rules.md).
@Component({
  selector: 'app-guardian-house-rules',
  imports: [FormsModule, MarkdownView, Page, RouterLink, RuleEditor, TranslatePipe],
  templateUrl: './house-rules.html',
})
export class GuardianHouseRules {
  private readonly guardians = inject(GuardiansService);
  private readonly groups = inject(GroupsService);
  private readonly houseRules = inject(HouseRulesService);
  private readonly translation = inject(TranslationService);
  private readonly injector = inject(Injector);
  // The button that opened the editor, focused again when it closes.
  private editorTrigger: HTMLElement | null = null;

  protected readonly children = resource({ loader: () => this.guardians.listMyChildren() });
  protected readonly groupList = resource({ loader: () => this.groups.listMyGroups() });

  private readonly childList = computed((): ChildSummary[] =>
    this.children.hasValue() ? this.children.value() : [],
  );
  protected readonly householdOptions = computed((): ScopeOption[] =>
    (this.groupList.hasValue() ? this.groupList.value() : []).map((group) => ({
      kind: 'Group',
      id: group.id,
      key: keyOf({ kind: 'Group', id: group.id }),
      label: group.name,
    })),
  );
  protected readonly childOptions = computed((): ScopeOption[] =>
    this.childList().map((child) => ({
      kind: 'Child',
      id: child.id,
      key: keyOf({ kind: 'Child', id: child.id }),
      label: child.name.givenName,
    })),
  );
  private readonly options = computed(() => [...this.householdOptions(), ...this.childOptions()]);
  protected readonly listsLoading = computed(
    () => this.children.isLoading() || this.groupList.isLoading(),
  );
  protected readonly listsFailed = computed(
    () => this.children.error() !== undefined || this.groupList.error() !== undefined,
  );

  protected readonly selectedKey = linkedSignal(() => this.options()[0]?.key);
  protected readonly scope = computed(
    (): RuleScope | undefined => {
      const option = this.options().find((o) => o.key === this.selectedKey());
      return option ? { kind: option.kind, id: option.id } : undefined;
    },
    { equal: (a, b) => a?.kind === b?.kind && a?.id === b?.id },
  );

  protected readonly book = resource({
    params: () => this.scope(),
    loader: ({ params }) => this.houseRules.listRules(params),
  });
  // Names for the status chips: the members of a household (its children may belong to another
  // family), or the guardian's own children. Best effort -- a failure leaves the plain list.
  private readonly memberNames = resource({
    params: () => this.scope(),
    loader: async ({ params }): Promise<Record<string, string>> => {
      if (params.kind !== 'Group') {
        return {};
      }

      const group = await this.groups.getGroup(params.id);
      return Object.fromEntries(group.members.map((m) => [m.userId, m.givenName]));
    },
  });
  private readonly names = computed((): Record<string, string> => ({
    ...Object.fromEntries(this.childList().map((c) => [c.id, c.name.givenName])),
    ...(this.memberNames.hasValue() ? this.memberNames.value() : {}),
  }));
  private readonly myChildIds = computed(() => new Set(this.childList().map((c) => c.id)));

  protected readonly loaded = computed((): RuleBook | undefined =>
    this.book.hasValue() ? this.book.value() : undefined,
  );
  protected readonly canManage = computed(() => this.loaded()?.access === 'Manage');
  protected readonly printLink = computed(() => {
    const scope = this.scope();

    if (!scope) {
      return null;
    }

    return scope.kind === 'Group'
      ? ['/guardian/house-rules/print/groups', scope.id]
      : ['/guardian/house-rules/print/children', scope.id];
  });

  protected readonly editing = signal<Editing>(null);
  protected readonly confirmingRemove = signal<string | null>(null);
  // Keyed by rule id, 'new' for the add dialog.
  protected readonly action = createAction<string>();
  protected readonly statusKeys = STATUS_KEYS;

  protected selectScope(key: string): void {
    this.selectedKey.set(key);
    this.editing.set(null);
    this.confirmingRemove.set(null);
    this.action.reset();
  }

  // A household child from another family has no name until the group's members load.
  protected nameOf(childId: string): string {
    return this.names()[childId] ?? this.translation.translate('houseRules.aChild');
  }

  protected statusOf(acknowledgement: RuleAcknowledgement): AcknowledgementStatus {
    if (acknowledgement.isUpToDate) {
      return 'upToDate';
    }

    return acknowledgement.acknowledgedRevision === null ? 'new' : 'changed';
  }

  // Only a Manage caller who is the child's own guardian may tick for them (the API checks the same).
  protected canReadWith(acknowledgement: RuleAcknowledgement): boolean {
    return (
      this.canManage() &&
      !acknowledgement.isUpToDate &&
      this.myChildIds().has(acknowledgement.childId)
    );
  }

  protected openAdd(event: Event): void {
    this.openEditor(event, { kind: 'new' });
  }

  protected openEdit(event: Event, rule: Rule): void {
    this.openEditor(event, { kind: 'edit', rule });
  }

  private openEditor(event: Event, editing: NonNullable<Editing>): void {
    this.editorTrigger = event.currentTarget instanceof HTMLElement ? event.currentTarget : null;
    this.action.reset();
    this.editing.set(editing);
  }

  protected editorRule(editing: NonNullable<Editing>): Rule | null {
    return editing.kind === 'edit' ? editing.rule : null;
  }

  protected editorError(editing: NonNullable<Editing>): string | null {
    const state = this.action.state();
    const id = editing.kind === 'edit' ? editing.rule.id : 'new';
    return state.status === 'error' && state.id === id ? state.message : null;
  }

  protected closeEditor(): void {
    this.editing.set(null);
    this.action.reset();
    this.refocusTrigger();
  }

  private refocusTrigger(): void {
    const trigger = this.editorTrigger;
    this.editorTrigger = null;
    afterNextRender(() => trigger?.isConnected && trigger.focus(), { injector: this.injector });
  }

  protected async saveRule(editing: NonNullable<Editing>, result: RuleEditorResult): Promise<void> {
    const scope = this.scope();

    if (!scope) {
      return;
    }

    const id = editing.kind === 'edit' ? editing.rule.id : 'new';
    const saved = await this.action.run(
      id,
      async () => {
        const book =
          editing.kind === 'edit'
            ? await this.houseRules.editRule(
                scope,
                editing.rule.id,
                result.content,
                result.requireReacknowledgement,
              )
            : await this.houseRules.addRule(scope, result.content);
        this.showBook(scope, book);
      },
      'houseRules.saveError',
    );

    if (saved) {
      this.editing.set(null);
      this.refocusTrigger();
    }
  }

  protected async remove(rule: Rule): Promise<void> {
    const scope = this.scope();

    if (!scope) {
      return;
    }

    await this.action.run(
      rule.id,
      async () => {
        await this.houseRules.removeRule(scope, rule.id);
        this.confirmingRemove.set(null);
        this.updateBook((book) => ({ ...book, rules: book.rules.filter((r) => r.id !== rule.id) }));
      },
      'houseRules.removeError',
    );
  }

  protected async move(rule: Rule, offset: -1 | 1): Promise<void> {
    const scope = this.scope();
    const book = this.loaded();

    if (!scope || !book) {
      return;
    }

    const order = book.rules.map((r) => r.id);
    const from = order.indexOf(rule.id);
    const to = from + offset;

    if (to < 0 || to >= order.length) {
      return;
    }

    order.splice(to, 0, ...order.splice(from, 1));

    await this.action.run(
      rule.id,
      async () => this.showBook(scope, await this.houseRules.reorderRules(scope, order)),
      'houseRules.reorderError',
    );
  }

  protected async readWith(rule: Rule, childId: string): Promise<void> {
    const scope = this.scope();

    if (!scope) {
      return;
    }

    await this.action.run(
      rule.id,
      async () => {
        await this.houseRules.acknowledge(scope, rule.id, rule.revision, childId);
        this.book.reload();
      },
      'houseRules.acknowledgeError',
    );
  }

  protected ruleError(ruleId: string): string | null {
    const state = this.action.state();
    return state.status === 'error' && state.id === ruleId && this.editing() === null
      ? state.message
      : null;
  }

  // A write's answer is only shown while its scope is still the one on screen: the picker is
  // disabled during a write, but this keeps a late answer from replacing another scope's rules.
  private showBook(scope: RuleScope, book: RuleBook): void {
    const current = this.scope();

    if (current && keyOf(current) === keyOf(scope)) {
      this.book.set(book);
    }
  }

  // Only once loaded: setting the resource mid-load would cancel the load.
  private updateBook(change: (book: RuleBook) => RuleBook): void {
    if (this.book.hasValue() && !this.book.isLoading()) {
      this.book.set(change(this.book.value()));
    }
  }
}

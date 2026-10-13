import { Component, computed, inject, linkedSignal, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';

import { FeaturesService } from '../../../core/features.service';
import { ChildSummary, GuardiansService } from '../../../core/guardians.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';
import type { WarningKind } from '../../../core/transition-warnings';
import {
  ChildTransitionWarnings,
  TransitionWarningsService,
} from '../../../core/transition-warnings.service';
import { Page } from '../../../shared/page/page';
import { TimeSelect } from '../../../shared/time-select/time-select';
import { Toggle } from '../../../shared/toggle/toggle';
import { LeadChips } from './lead-chips/lead-chips';
import { WarningExceptions } from './warning-exceptions/warning-exceptions';
import { ALL_KINDS, WarningDraft, draftFrom, isDirty, requestFrom } from './warning-settings';

const KIND_LABELS = {
  Event: 'transitionWarnings.kinds.Event',
  Task: 'transitionWarnings.kinds.Task',
  DropOff: 'transitionWarnings.kinds.DropOff',
  PickUp: 'transitionWarnings.kinds.PickUp',
} as const satisfies Record<WarningKind, string>;

const PICKUP_KINDS: readonly WarningKind[] = ['DropOff', 'PickUp'];

/**
 * Per-child transition warnings (docs/backend/analysis/transition-warnings.md): the "Warn ...
 * before changes" switch, and once it is on the lead times, kinds, quiet hours and exceptions. The
 * switch is part of the form, saved with the same PUT, so a guardian sees what they switch on
 * before it reaches the child. Opens on `?child=<id>` (the manage-children link), else the first.
 */
@Component({
  selector: 'app-guardian-transition-warnings',
  imports: [FormsModule, TranslatePipe, Page, Toggle, TimeSelect, LeadChips, WarningExceptions],
  templateUrl: './transition-warnings.html',
})
export class GuardianTransitionWarnings {
  private readonly guardians = inject(GuardiansService);
  private readonly api = inject(TransitionWarningsService);
  private readonly translation = inject(TranslationService);
  protected readonly features = inject(FeaturesService);
  private readonly requestedChildId =
    inject(ActivatedRoute, { optional: true })?.snapshot.queryParamMap.get('child') ?? undefined;

  protected readonly kindLabels = KIND_LABELS;

  protected readonly children = resource({ loader: () => this.guardians.listMyChildren() });
  protected readonly childList = computed((): ChildSummary[] =>
    this.children.hasValue() ? this.children.value() : [],
  );
  protected readonly selectedChildId = linkedSignal(() => {
    const list = this.childList();
    return (list.find((child) => child.id === this.requestedChildId) ?? list[0])?.id;
  });
  protected readonly childName = computed(
    () =>
      this.childList().find((child) => child.id === this.selectedChildId())?.name.givenName ?? '',
  );

  protected readonly settings = resource({
    params: () => this.selectedChildId(),
    loader: ({ params }) => this.api.getForChild(params),
  });
  protected readonly saved = computed(() =>
    this.settings.hasValue() ? this.settings.value() : undefined,
  );
  protected readonly draft = linkedSignal<ChildTransitionWarnings | undefined, WarningDraft | null>(
    {
      source: this.saved,
      computation: (settings) => (settings ? draftFrom(settings) : null),
    },
  );

  protected readonly saving = signal(false);
  protected readonly status = signal<'saved' | 'error' | null>(null);

  // Drop-off and pickup only mean something while pickups are on.
  protected readonly kinds = computed(() =>
    this.features.enabled('pickups')
      ? ALL_KINDS
      : ALL_KINDS.filter((kind) => !PICKUP_KINDS.includes(kind)),
  );

  protected readonly dirty = computed(() => {
    const draft = this.draft();
    const saved = this.saved();
    return !!draft && !!saved && isDirty(draft, saved);
  });
  protected readonly canSave = computed(() => {
    const draft = this.draft();
    return (
      !!draft &&
      this.dirty() &&
      !this.saving() &&
      draft.leadMinutes.length > 0 &&
      // A kind hidden while pickups are off doesn't count: the guardian can't see it is on.
      (!draft.enabled || draft.kinds.some((kind) => this.kinds().includes(kind))) &&
      (!draft.quietOn || draft.quietFrom !== draft.quietTo)
    );
  });

  // "15 min, 5 min and 1 min", in the page's language.
  protected readonly times = computed(() =>
    new Intl.ListFormat(this.translation.language(), { type: 'conjunction' }).format(
      (this.draft()?.leadMinutes ?? []).map((minutes) =>
        this.translation.translate('transitionWarnings.leadTimes.minutes', { minutes }),
      ),
    ),
  );

  protected selectChild(childId: string): void {
    this.selectedChildId.set(childId);
    this.status.set(null);
  }

  protected update(change: Partial<WarningDraft>): void {
    this.draft.update((draft) => (draft ? { ...draft, ...change } : draft));
    this.status.set(null);
  }

  protected hasKind(kind: WarningKind): boolean {
    return this.draft()?.kinds.includes(kind) ?? false;
  }

  protected setKind(kind: WarningKind, on: boolean): void {
    const kinds = this.draft()?.kinds ?? [];
    this.update({ kinds: on ? [...kinds, kind] : kinds.filter((k) => k !== kind) });
  }

  protected async save(): Promise<void> {
    const draft = this.draft();
    const childId = this.selectedChildId();
    if (!draft || !childId || !this.canSave()) {
      return;
    }

    this.saving.set(true);
    this.status.set(null);
    try {
      const updated = await this.api.configure(childId, requestFrom(draft));
      if (this.selectedChildId() === childId) {
        this.settings.set(updated);
        this.status.set('saved');
      }
    } catch {
      this.status.set('error');
    } finally {
      this.saving.set(false);
    }
  }

  // An exception saved or removed: the rest of the draft stays as the guardian left it.
  protected onExceptionSaved(updated: ChildTransitionWarnings): void {
    this.keepDraft(() => this.settings.set(updated));
  }

  protected onExceptionRemoved(itemId: string): void {
    const saved = this.saved();
    if (saved) {
      this.keepDraft(() =>
        this.settings.set({
          ...saved,
          overrides: saved.overrides.filter((o) => o.itemId !== itemId),
        }),
      );
    }
  }

  private keepDraft(change: () => void): void {
    const draft = this.draft();
    change();
    if (draft) {
      this.draft.set(draft);
    }
  }
}

import { Component, computed, inject, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';

import {
  AiAssistantService,
  AiServedWindow,
  AiSessionStatus,
  AiSessionView,
  SERVED_WINDOW_DAYS,
} from '../../../../core/ai-assistant.service';
import { GuardiansService } from '../../../../core/guardians.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { MealSlot } from '../../../../core/mealplans.service';
import { createAction } from '../../../../shared/action-state/action-state';
import {
  SegmentedControl,
  SegmentedControlOption,
} from '../../../../shared/segmented-control/segmented-control';
import { Toggle } from '../../../../shared/toggle/toggle';
import { AiDataSharingNotice } from '../ai-data-sharing-notice/ai-data-sharing-notice';
import { readLastMealFilter, writeLastMealFilter } from './ai-meal-filter-storage';

const DRAFTING = 'Drafting' satisfies AiSessionStatus;

const SLOT_LABEL_KEYS: Record<MealSlot, string> = {
  Breakfast: 'mealplan.slots.breakfast',
  Lunch: 'mealplan.slots.lunch',
  Dinner: 'mealplan.slots.dinner',
  Snack: 'mealplan.slots.snack',
};

const ALL_SLOTS: readonly MealSlot[] = ['Breakfast', 'Lunch', 'Dinner', 'Snack'];

const SERVED_WINDOWS: readonly Exclude<AiServedWindow, 'Any'>[] = [
  'Last30Days',
  'Last60Days',
  'Last90Days',
];

// The backend keys a "no meals match the filter" rejection on ServedWithin, for both starting a
// session and sending a message (docs/backend/analysis/ai-assistant-meal-filter.md, Question 6).
function isNoMatchingMeals(error: unknown): boolean {
  if (!(error instanceof HttpErrorResponse) || error.status !== 400) {
    return false;
  }

  const details = (error.error as { details?: Record<string, string[]> } | null)?.details;
  return details?.['ServedWithin'] !== undefined;
}

function todayIsoDate(): string {
  return new Date().toISOString().slice(0, 10);
}

function addDaysIso(iso: string, days: number): string {
  const date = new Date(`${iso}T00:00:00Z`);
  date.setUTCDate(date.getUTCDate() + days);
  return date.toISOString().slice(0, 10);
}

// What the page loaded for the guardian's first child: its provider setup and its drafting session
// (null when there's none to resume). The page loads null when the guardian has no children.
interface AssistantChild {
  childId: string;
  hasProviderConfigured: boolean;
  dataSharingAcknowledged: boolean;
  session: AiSessionView | null;
}

@Component({
  selector: 'app-mealplan-ai-assistant',
  imports: [RouterLink, FormsModule, TranslatePipe, AiDataSharingNotice, Toggle, SegmentedControl],
  templateUrl: './ai-assistant.html',
})
export class MealplanAiAssistant {
  private readonly guardians = inject(GuardiansService);
  private readonly aiAssistant = inject(AiAssistantService);
  private readonly translation = inject(TranslationService);

  protected readonly slotLabelKeys = SLOT_LABEL_KEYS;
  protected readonly allSlots = ALL_SLOTS;

  protected readonly assistant = resource({ loader: () => this.load() });
  protected readonly lastOutcome = signal<'applied' | 'discarded' | null>(null);

  protected readonly fromDate = signal(todayIsoDate());
  protected readonly toDate = signal(addDaysIso(todayIsoDate(), 6));
  protected readonly selectedSlots = signal<Set<MealSlot>>(new Set<MealSlot>(['Dinner']));
  protected readonly notes = signal('');
  private readonly lastFilter = readLastMealFilter();
  protected readonly servedWindowDays = SERVED_WINDOW_DAYS;
  protected readonly ratedOnly = signal(this.lastFilter.ratedOnly);
  protected readonly servedWithin = signal<AiServedWindow>(this.lastFilter.servedWithin);
  protected readonly servedWithinOptions = computed(
    (): SegmentedControlOption<AiServedWindow>[] => {
      this.translation.language();
      return [
        {
          value: 'Any',
          label: this.translation.translate('mealplan.aiAssistant.start.servedWithinAll'),
        },
        ...SERVED_WINDOWS.map((window) => ({
          value: window,
          label: this.translation.translate('mealplan.aiAssistant.start.servedWithinDays', {
            days: SERVED_WINDOW_DAYS[window],
          }),
        })),
      ];
    },
  );
  protected readonly starting = createAction();

  protected readonly messageInput = signal('');
  protected readonly sending = createAction();

  protected readonly applying = createAction();

  protected readonly confirmingDiscard = signal(false);
  protected readonly discarding = createAction();

  protected isSlotSelected(slot: MealSlot): boolean {
    return this.selectedSlots().has(slot);
  }

  protected toggleSlot(slot: MealSlot): void {
    this.selectedSlots.update((current) => {
      const next = new Set(current);
      if (next.has(slot)) {
        next.delete(slot);
      } else {
        next.add(slot);
      }
      return next;
    });
  }

  protected async startSession(): Promise<void> {
    const childId = this.childId();
    const slots = [...this.selectedSlots()];

    if (!childId || slots.length === 0) {
      return;
    }

    await this.starting.run(
      true,
      async () => {
        const session = await this.aiAssistant.startSession(childId, {
          from: this.fromDate(),
          to: this.toDate(),
          slots,
          mustIncludeMealIds: [],
          notes: this.notes().trim(),
          ratedOnly: this.ratedOnly(),
          servedWithin: this.servedWithin(),
        });
        writeLastMealFilter({ ratedOnly: this.ratedOnly(), servedWithin: this.servedWithin() });
        this.setSession(session);
        this.lastOutcome.set(null);
      },
      (error) =>
        isNoMatchingMeals(error)
          ? 'mealplan.aiAssistant.start.noMatchingMeals'
          : 'mealplan.aiAssistant.start.startError',
    );
  }

  protected async sendMessage(): Promise<void> {
    const childId = this.childId();
    const text = this.messageInput().trim();

    if (!childId || !text) {
      return;
    }

    await this.sending.run(
      true,
      async () => {
        this.setSession(await this.aiAssistant.sendMessage(childId, text));
        this.messageInput.set('');
      },
      (error) =>
        isNoMatchingMeals(error)
          ? 'mealplan.aiAssistant.session.noMatchingMeals'
          : 'mealplan.aiAssistant.session.sendError',
    );
  }

  protected async applyDraft(): Promise<void> {
    const childId = this.childId();

    if (!childId) {
      return;
    }

    await this.applying.run(
      true,
      async () => {
        // The response's Status is now Applied, not Drafting -- the chat UI is only ever shown for
        // a Drafting session, so this clears the session rather than storing the terminal one, or
        // the @if (child.session; as current) branch would stay truthy forever (it only checks
        // presence, not status) and never fall through to the outcome view.
        await this.aiAssistant.applyDraft(childId);
        this.setSession(null);
        this.lastOutcome.set('applied');
      },
      'mealplan.aiAssistant.session.applyError',
    );
  }

  protected requestDiscard(): void {
    this.discarding.reset();
    this.confirmingDiscard.set(true);
  }

  protected cancelDiscard(): void {
    this.confirmingDiscard.set(false);
  }

  protected async confirmDiscard(): Promise<void> {
    const childId = this.childId();

    if (!childId) {
      return;
    }

    await this.discarding.run(
      true,
      async () => {
        await this.aiAssistant.discardSession(childId);
        this.setSession(null);
        this.lastOutcome.set('discarded');
        this.confirmingDiscard.set(false);
      },
      'mealplan.aiAssistant.session.discardError',
    );
  }

  protected startNewSession(): void {
    this.setSession(null);
    this.lastOutcome.set(null);
    this.starting.reset();
  }

  // The i18n key describing a session's meal filter, or null when it has none.
  protected filterSummaryKey(session: AiSessionView): string | null {
    if (session.servedWithin !== 'Any') {
      return session.ratedOnly
        ? 'mealplan.aiAssistant.session.filterRatedServed'
        : 'mealplan.aiAssistant.session.filterServed';
    }

    return session.ratedOnly ? 'mealplan.aiAssistant.session.filterRated' : null;
  }

  protected onDataSharingAcknowledged(): void {
    this.assistant.update((current) => current && { ...current, dataSharingAcknowledged: true });
  }

  private childId(): string | undefined {
    return this.assistant.hasValue() ? this.assistant.value()?.childId : undefined;
  }

  private setSession(session: AiSessionView | null): void {
    this.assistant.update((current) => current && { ...current, session });
  }

  private async load(): Promise<AssistantChild | null> {
    const children = await this.guardians.listMyChildren();
    const [firstChild] = children;

    if (!firstChild) {
      return null;
    }

    const childId = firstChild.id;
    const providers = await this.aiAssistant.listProviders(childId);

    return {
      childId,
      hasProviderConfigured: providers.activeProvider !== null,
      dataSharingAcknowledged: providers.dataSharingAcknowledgedAt !== null,
      session: await this.findDraftingSession(childId),
    };
  }

  private async findDraftingSession(childId: string): Promise<AiSessionView | null> {
    try {
      const current = await this.aiAssistant.getCurrentSession(childId);
      return current.status === DRAFTING ? current : null;
    } catch (err) {
      if (err instanceof HttpErrorResponse && err.status === 404) {
        return null;
      }

      throw err;
    }
  }
}

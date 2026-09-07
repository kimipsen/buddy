import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';

import { AiAssistantService, AiSessionView } from '../../../../core/ai-assistant.service';
import { GuardiansService } from '../../../../core/guardians.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { MealSlot } from '../../../../core/mealplans.service';

const DRAFTING = 0;

const SLOT_LABEL_KEYS: Record<MealSlot, string> = {
  0: 'mealplan.slots.breakfast',
  1: 'mealplan.slots.lunch',
  2: 'mealplan.slots.dinner',
  3: 'mealplan.slots.snack'
};

const ALL_SLOTS: readonly MealSlot[] = [0, 1, 2, 3];

function todayIsoDate(): string {
  return new Date().toISOString().slice(0, 10);
}

function addDaysIso(iso: string, days: number): string {
  const date = new Date(`${iso}T00:00:00Z`);
  date.setUTCDate(date.getUTCDate() + days);
  return date.toISOString().slice(0, 10);
}

@Component({
  selector: 'app-mealplan-ai-assistant',
  imports: [RouterLink, FormsModule, TranslatePipe],
  templateUrl: './ai-assistant.html'
})
export class MealplanAiAssistant implements OnInit {
  private readonly guardians = inject(GuardiansService);
  private readonly aiAssistant = inject(AiAssistantService);

  protected readonly slotLabelKeys = SLOT_LABEL_KEYS;
  protected readonly allSlots = ALL_SLOTS;

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly hasChildren = signal(true);
  protected readonly hasProviderConfigured = signal(true);

  protected readonly session = signal<AiSessionView | null>(null);
  protected readonly lastOutcome = signal<'applied' | 'discarded' | null>(null);

  protected readonly fromDate = signal(todayIsoDate());
  protected readonly toDate = signal(addDaysIso(todayIsoDate(), 6));
  protected readonly selectedSlots = signal<Set<MealSlot>>(new Set<MealSlot>([2]));
  protected readonly notes = signal('');
  protected readonly starting = signal(false);
  protected readonly startError = signal<string | null>(null);

  protected readonly messageInput = signal('');
  protected readonly sending = signal(false);
  protected readonly sendError = signal<string | null>(null);

  protected readonly applying = signal(false);
  protected readonly applyError = signal<string | null>(null);

  protected readonly confirmingDiscard = signal(false);
  protected readonly discarding = signal(false);
  protected readonly discardError = signal<string | null>(null);

  private childId: string | null = null;

  ngOnInit(): void {
    void this.load();
  }

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
    const childId = this.childId;
    const slots = [...this.selectedSlots()];

    if (!childId || slots.length === 0) {
      return;
    }

    this.starting.set(true);
    this.startError.set(null);

    try {
      const session = await this.aiAssistant.startSession(childId, {
        from: this.fromDate(),
        to: this.toDate(),
        slots,
        mustIncludeMealIds: [],
        notes: this.notes().trim() || null
      });
      this.session.set(session);
      this.lastOutcome.set(null);
    } catch {
      this.startError.set('mealplan.aiAssistant.start.startError');
    } finally {
      this.starting.set(false);
    }
  }

  protected async sendMessage(): Promise<void> {
    const childId = this.childId;
    const text = this.messageInput().trim();

    if (!childId || !text) {
      return;
    }

    this.sending.set(true);
    this.sendError.set(null);

    try {
      this.session.set(await this.aiAssistant.sendMessage(childId, text));
      this.messageInput.set('');
    } catch {
      this.sendError.set('mealplan.aiAssistant.session.sendError');
    } finally {
      this.sending.set(false);
    }
  }

  protected async applyDraft(): Promise<void> {
    const childId = this.childId;

    if (!childId) {
      return;
    }

    this.applying.set(true);
    this.applyError.set(null);

    try {
      // The response's Status is now Applied, not Drafting -- the chat UI is only ever shown for
      // a Drafting session, so this clears session() rather than storing the terminal one, or the
      // @if (session(); as current) branch below would stay truthy forever (it only checks
      // presence, not status) and never fall through to the outcome view.
      await this.aiAssistant.applyDraft(childId);
      this.session.set(null);
      this.lastOutcome.set('applied');
    } catch {
      this.applyError.set('mealplan.aiAssistant.session.applyError');
    } finally {
      this.applying.set(false);
    }
  }

  protected requestDiscard(): void {
    this.discardError.set(null);
    this.confirmingDiscard.set(true);
  }

  protected cancelDiscard(): void {
    this.confirmingDiscard.set(false);
  }

  protected async confirmDiscard(): Promise<void> {
    const childId = this.childId;

    if (!childId) {
      return;
    }

    this.discarding.set(true);
    this.discardError.set(null);

    try {
      await this.aiAssistant.discardSession(childId);
      this.session.set(null);
      this.lastOutcome.set('discarded');
      this.confirmingDiscard.set(false);
    } catch {
      this.discardError.set('mealplan.aiAssistant.session.discardError');
    } finally {
      this.discarding.set(false);
    }
  }

  protected startNewSession(): void {
    this.session.set(null);
    this.lastOutcome.set(null);
    this.startError.set(null);
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      const children = await this.guardians.listMyChildren();

      if (children.length === 0) {
        this.hasChildren.set(false);
        return;
      }

      this.hasChildren.set(true);
      this.childId = children[0].id;

      const providers = await this.aiAssistant.listProviders(this.childId);
      this.hasProviderConfigured.set(providers.activeProvider !== null);

      try {
        const current = await this.aiAssistant.getCurrentSession(this.childId);
        this.session.set(current.status === DRAFTING ? current : null);
      } catch (err) {
        if (err instanceof HttpErrorResponse && err.status === 404) {
          this.session.set(null);
        } else {
          throw err;
        }
      }
    } catch {
      this.error.set('mealplan.aiAssistant.loadError');
    } finally {
      this.loading.set(false);
    }
  }
}

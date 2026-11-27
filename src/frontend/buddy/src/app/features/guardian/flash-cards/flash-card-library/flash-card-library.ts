import { Component, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import {
  FlashCardSet,
  FlashCardSetKind,
  FlashCardsService,
} from '../../../../core/flash-cards.service';
import { builtInNameKey, clockAriaParams } from '../../../../core/flash-card-display';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { createAction } from '../../../../shared/action-state/action-state';
import { Card } from '../../../../shared/card/card';
import { ClockFace } from '../../../../shared/clock-face/clock-face';
import { SegmentedControl } from '../../../../shared/segmented-control/segmented-control';
import {
  KINDS,
  MATH_OPERATIONS,
  MathOperation,
  exampleCard,
  isValidLimit,
  limitRange,
} from '../math-example';

interface SetDraft {
  // Undefined for a set not saved yet.
  id: string | undefined;
  name: string;
  kind: FlashCardSetKind;
  operation: MathOperation;
  limit: string;
}

interface CardDraft {
  // Undefined for a card not saved yet.
  id: string | undefined;
  question: string;
  answer: string;
}

const DEFAULT_LIMIT: Record<MathOperation, string> = {
  Addition: '20',
  Subtraction: '20',
  Multiplication: '7',
  Division: '7',
};

const OPERATION_KEYS: Record<MathOperation, string> = {
  Addition: 'flashCards.operation.Addition',
  Subtraction: 'flashCards.operation.Subtraction',
  Multiplication: 'flashCards.operation.Multiplication',
  Division: 'flashCards.operation.Division',
};

const KIND_KEYS: Record<FlashCardSetKind, string> = {
  Math: 'flashCards.kind.Math',
  Text: 'flashCards.kind.Text',
  BuiltIn: 'flashCards.kind.BuiltIn',
};

const SAVE_ERROR = 'flashCards.guardian.saveError';

// The guardian's library of card sets: list, create (a maths rule or a text set), edit, delete
// with a confirmation, and an inline cards editor for text sets. The page owns the list.
@Component({
  selector: 'app-flash-card-library',
  imports: [Card, ClockFace, FormsModule, SegmentedControl, TranslatePipe],
  templateUrl: './flash-card-library.html',
})
export class FlashCardLibrary {
  private readonly flashCards = inject(FlashCardsService);
  private readonly translation = inject(TranslationService);

  readonly sets = input.required<FlashCardSet[]>();
  readonly setSaved = output<FlashCardSet>();
  readonly setDeleted = output<string>();

  protected readonly operations = MATH_OPERATIONS;
  protected readonly action = createAction<string>();
  protected readonly draft = signal<SetDraft | null>(null);
  protected readonly cardDraft = signal<CardDraft | null>(null);
  protected readonly confirmingDelete = signal<string | null>(null);

  // Own sets first; the catalogue sets are a read-only group below. Any kind but Math and Text is
  // read-only, so a kind this build does not know is never editable.
  protected readonly ownSets = computed(() => this.sets().filter((set) => !this.isReadOnly(set)));
  protected readonly readOnlySets = computed(() =>
    this.sets().filter((set) => this.isReadOnly(set)),
  );

  protected readonly kindOptions = computed(() =>
    KINDS.map((kind) => ({ value: kind, label: this.translation.translate(KIND_KEYS[kind]) })),
  );
  protected readonly operationOptions = computed(() =>
    MATH_OPERATIONS.map((operation) => ({
      value: operation,
      label: this.translation.translate(OPERATION_KEYS[operation]),
    })),
  );

  // The set being edited, as the page last knows it: card changes arrive through the sets input.
  protected readonly editedSet = computed(() => {
    const id = this.draft()?.id;
    return id === undefined ? undefined : this.sets().find((set) => set.id === id);
  });
  protected readonly example = computed(() => {
    const draft = this.draft();
    const limit = Number(draft?.limit);

    return draft?.kind === 'Math' && isValidLimit(draft.operation, limit)
      ? exampleCard(draft.operation, limit)
      : null;
  });

  protected isReadOnly(set: FlashCardSet): boolean {
    return set.kind !== 'Math' && set.kind !== 'Text';
  }

  protected kindKey(set: FlashCardSet): string {
    return (KIND_KEYS as Partial<Record<string, string>>)[set.kind] ?? KIND_KEYS.BuiltIn;
  }

  protected displayName(set: FlashCardSet): string {
    const key = builtInNameKey(set.builtInKey);
    return key ? this.translation.translate(key) : set.name;
  }

  protected clockLabel(clock: { hour: number; minute: number }): string {
    return this.translation.translate('flashCards.clock.ariaLabel', clockAriaParams(clock));
  }

  protected operationLabel(operation: MathOperation): string {
    return OPERATION_KEYS[operation];
  }

  protected summary(set: FlashCardSet): { key: string; params: Record<string, string | number> } {
    const math = set.math;

    if (!math) {
      return { key: 'flashCards.guardian.library.cardCount', params: { count: set.cards.length } };
    }

    const key =
      math.operation === 'Multiplication'
        ? 'flashCards.guardian.library.mathSummaryTable'
        : math.operation === 'Division'
          ? 'flashCards.guardian.library.mathSummaryBy'
          : 'flashCards.guardian.library.mathSummary';

    return {
      key,
      params: {
        operation: this.translation.translate(OPERATION_KEYS[math.operation]),
        limit: math.limit,
      },
    };
  }

  protected limitLabelKey(operation: MathOperation): string {
    return operation === 'Multiplication'
      ? 'flashCards.guardian.set.tableLabel'
      : operation === 'Division'
        ? 'flashCards.guardian.set.divisorLabel'
        : 'flashCards.guardian.set.limitLabel';
  }

  protected limitBounds(operation: MathOperation): { min: number; max: number } {
    return limitRange(operation);
  }

  protected openNew(): void {
    this.action.reset();
    this.cardDraft.set(null);
    this.confirmingDelete.set(null);
    this.draft.set({
      id: undefined,
      name: '',
      kind: 'Math',
      operation: 'Addition',
      limit: DEFAULT_LIMIT['Addition'],
    });
  }

  protected openEdit(set: FlashCardSet): void {
    this.action.reset();
    this.cardDraft.set(null);
    this.confirmingDelete.set(null);
    this.draft.set({
      id: set.id,
      name: set.name,
      kind: set.kind,
      operation: set.math?.operation ?? 'Addition',
      limit: String(set.math?.limit ?? DEFAULT_LIMIT['Addition']),
    });
  }

  protected cancel(): void {
    this.action.reset();
    this.cardDraft.set(null);
    this.draft.set(null);
  }

  protected patchDraft(patch: Partial<SetDraft>): void {
    this.draft.update((draft) => (draft ? { ...draft, ...patch } : draft));
  }

  // Picking another operation resets the number: a limit fine for addition is out of range for a
  // table.
  protected setOperation(operation: MathOperation): void {
    this.patchDraft({ operation, limit: DEFAULT_LIMIT[operation] });
  }

  protected async save(): Promise<void> {
    const draft = this.draft();

    if (!draft) {
      return;
    }

    const name = draft.name.trim();
    const limit = Number(draft.limit);
    const math = draft.kind === 'Math' ? { operation: draft.operation, limit } : null;

    if (!name || (math && !isValidLimit(math.operation, limit))) {
      this.action.fail('set', SAVE_ERROR);
      return;
    }

    let saved: FlashCardSet | undefined;
    const ok = await this.action.run(
      'set',
      async () => {
        saved = draft.id
          ? await this.flashCards.updateSet(draft.id, math ? { name, math } : { name })
          : await this.flashCards.createSet(
              math ? { name, kind: 'Math', math } : { name, kind: 'Text' },
            );
      },
      SAVE_ERROR,
    );

    if (!ok || !saved) {
      return;
    }

    this.setSaved.emit(saved);
    // A new text set stays open so its cards can be added; anything else is done.
    this.draft.set(
      saved.kind === 'Text' && !draft.id ? { ...draft, id: saved.id, name: saved.name } : null,
    );
  }

  protected askDelete(setId: string): void {
    this.action.reset();
    this.confirmingDelete.set(setId);
  }

  protected cancelDelete(): void {
    this.confirmingDelete.set(null);
  }

  protected async confirmDelete(set: FlashCardSet): Promise<void> {
    const ok = await this.action.run(
      `delete:${set.id}`,
      () => this.flashCards.deleteSet(set.id),
      SAVE_ERROR,
    );

    if (!ok) {
      return;
    }

    this.confirmingDelete.set(null);

    if (this.draft()?.id === set.id) {
      this.draft.set(null);
    }

    this.setDeleted.emit(set.id);
  }

  protected newCard(): void {
    this.cardDraft.set({ id: undefined, question: '', answer: '' });
  }

  protected editCard(card: { id: string; question: string; answer: string }): void {
    this.cardDraft.set({ id: card.id, question: card.question, answer: card.answer });
  }

  protected patchCard(patch: Partial<CardDraft>): void {
    this.cardDraft.update((card) => (card ? { ...card, ...patch } : card));
  }

  protected cancelCard(): void {
    this.cardDraft.set(null);
  }

  protected async saveCard(): Promise<void> {
    const setId = this.draft()?.id;
    const card = this.cardDraft();

    if (!setId || !card) {
      return;
    }

    const request = { question: card.question.trim(), answer: card.answer.trim() };

    if (!request.question || !request.answer) {
      this.action.fail('card', SAVE_ERROR);
      return;
    }

    let saved: FlashCardSet | undefined;
    const ok = await this.action.run(
      'card',
      async () => {
        saved = card.id
          ? await this.flashCards.updateCard(setId, card.id, request)
          : await this.flashCards.addCard(setId, request);
      },
      SAVE_ERROR,
    );

    if (ok && saved) {
      this.setSaved.emit(saved);
      this.cardDraft.set(null);
    }
  }

  protected async removeCard(set: FlashCardSet, cardId: string): Promise<void> {
    const ok = await this.action.run(
      `remove:${cardId}`,
      () => this.flashCards.removeCard(set.id, cardId),
      SAVE_ERROR,
    );

    if (ok) {
      this.setSaved.emit({
        ...set,
        cards: set.cards.filter((card) => card.id !== cardId),
        uniqueCardCount: set.cards.filter((card) => card.id !== cardId).length,
      });
    }
  }
}

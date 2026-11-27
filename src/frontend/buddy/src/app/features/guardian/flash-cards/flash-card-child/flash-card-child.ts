import { NgTemplateOutlet } from '@angular/common';
import {
  Component,
  computed,
  inject,
  input,
  linkedSignal,
  output,
  resource,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';

import {
  ChildFlashCards,
  EnabledFlashCardSet,
  FlashCardSet,
  FlashCardTierRequest,
  FlashCardsService,
} from '../../../../core/flash-cards.service';
import { builtInNameKey } from '../../../../core/flash-card-display';
import { ChildSummary } from '../../../../core/guardians.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { createAction } from '../../../../shared/action-state/action-state';
import { Card } from '../../../../shared/card/card';

// FlashCardLimits on the backend.
export const MIN_TIERS = 1;
export const MAX_TIERS = 5;
export const MIN_TIER_CARDS = 3;
export const MAX_TIER_CARDS = 50;
export const MIN_TIER_STARS = 1;
export const MAX_TIER_STARS = 20;

export interface TierRow {
  // Undefined for a row added in this draft; the server assigns its id on save.
  id: string | undefined;
  cardCount: string;
  stars: string;
}

const DEFAULT_ROW: TierRow = { id: undefined, cardCount: '5', stars: '1' };
const SAVE_ERROR = 'flashCards.guardian.saveError';
const INVALID_TIERS = 'flashCards.guardian.child.tierInvalid';

function isWhole(value: string, min: number, max: number): boolean {
  const number = Number(value);
  return value.trim() !== '' && Number.isInteger(number) && number >= min && number <= max;
}

export function tiersValid(rows: TierRow[]): boolean {
  return (
    rows.length >= MIN_TIERS &&
    rows.length <= MAX_TIERS &&
    rows.every(
      (row) =>
        isWhole(row.cardCount, MIN_TIER_CARDS, MAX_TIER_CARDS) &&
        isWhole(row.stars, MIN_TIER_STARS, MAX_TIER_STARS),
    )
  );
}

// A guardian's setup for one child: which sets they can play (with the game lengths, each a card
// count and the stars it earns), and the games they have finished, newest first.
@Component({
  selector: 'app-flash-card-child',
  imports: [Card, FormsModule, NgTemplateOutlet, TranslatePipe],
  templateUrl: './flash-card-child.html',
})
export class FlashCardChild {
  private readonly flashCards = inject(FlashCardsService);
  private readonly translation = inject(TranslationService);

  readonly children = input.required<ChildSummary[]>();
  readonly childId = input<string | undefined>();
  // Every set in the library.
  readonly sets = input.required<FlashCardSet[]>();
  readonly childIdChange = output<string>();

  // A built-in set's name is translated by its key; any other set shows its own name.
  protected displayName(set: FlashCardSet): string {
    const key = builtInNameKey(set.builtInKey);
    return key ? this.translation.translate(key) : set.name;
  }

  protected readonly limits = {
    minCards: MIN_TIER_CARDS,
    maxCards: MAX_TIER_CARDS,
    minStars: MIN_TIER_STARS,
    maxStars: MAX_TIER_STARS,
  };
  protected readonly action = createAction<string>();
  protected readonly savedSetId = signal<string | null>(null);

  protected readonly data = resource({
    params: () => this.childId(),
    loader: ({ params }) => this.flashCards.getChildFlashCards(params),
  });
  protected readonly loaded = computed((): ChildFlashCards | undefined =>
    this.data.hasValue() ? this.data.value() : undefined,
  );
  protected readonly child = computed(() =>
    this.children().find((candidate) => candidate.id === this.childId()),
  );

  // Edits to the tiers of a set, by set id. A reload (switching child, enabling, disabling)
  // drops them, so the rows always start from what the server has.
  private readonly drafts = linkedSignal<ChildFlashCards | undefined, Record<string, TierRow[]>>({
    source: this.loaded,
    computation: () => ({}),
  });

  private readonly enabledById = computed(() => {
    const byId: Partial<Record<string, EnabledFlashCardSet>> = {};

    for (const enabled of this.loaded()?.enabledSets ?? []) {
      byId[enabled.setId] = enabled;
    }

    return byId;
  });
  protected readonly enabledSets = computed(() =>
    this.sets().filter((set) => this.enabledById()[set.id] !== undefined),
  );
  protected readonly availableSets = computed(() =>
    this.sets().filter((set) => this.enabledById()[set.id] === undefined),
  );
  protected readonly history = computed(() =>
    [...(this.loaded()?.games ?? [])].sort((a, b) => b.finishedOn.localeCompare(a.finishedOn)),
  );

  protected rows(set: FlashCardSet): TierRow[] {
    const draft = this.drafts()[set.id];

    if (draft) {
      return draft;
    }

    const enabled = this.enabledById()[set.id];

    return enabled
      ? enabled.tiers.map((tier) => ({
          id: tier.id,
          cardCount: String(tier.cardCount),
          stars: String(tier.stars),
        }))
      : [{ ...DEFAULT_ROW }];
  }

  private setRows(set: FlashCardSet, rows: TierRow[]): void {
    this.savedSetId.set(null);
    this.drafts.update((drafts) => ({ ...drafts, [set.id]: rows }));
  }

  // A number input's ngModel hands over a number (null when cleared), the rows hold text.
  protected patchRow(
    set: FlashCardSet,
    index: number,
    patch: { cardCount?: string | number | null; stars?: string | number | null },
  ): void {
    const text = (value: string | number | null | undefined): string | undefined =>
      value === undefined ? undefined : String(value ?? '');
    const cardCount = text(patch.cardCount);
    const stars = text(patch.stars);

    this.setRows(
      set,
      this.rows(set).map((row, i) =>
        i === index
          ? {
              ...row,
              ...(cardCount === undefined ? {} : { cardCount }),
              ...(stars === undefined ? {} : { stars }),
            }
          : row,
      ),
    );
  }

  protected addRow(set: FlashCardSet): void {
    const rows = this.rows(set);

    if (rows.length < MAX_TIERS) {
      this.setRows(set, [...rows, { ...DEFAULT_ROW }]);
    }
  }

  protected removeRow(set: FlashCardSet, index: number): void {
    const rows = this.rows(set);

    if (rows.length > MIN_TIERS) {
      this.setRows(
        set,
        rows.filter((_, i) => i !== index),
      );
    }
  }

  // The cards repeat in a game longer than the set's different cards.
  protected repeats(set: FlashCardSet, row: TierRow): boolean {
    return Number(row.cardCount) > set.uniqueCardCount;
  }

  protected selectChild(childId: string): void {
    this.action.reset();
    this.savedSetId.set(null);
    this.childIdChange.emit(childId);
  }

  // Switching a set on and saving the game lengths of a set already on are one call: a PUT of
  // the tiers.
  protected async saveTiers(set: FlashCardSet): Promise<void> {
    const childId = this.childId();
    const rows = this.rows(set);

    if (!childId) {
      return;
    }

    if (!tiersValid(rows)) {
      this.action.fail(set.id, INVALID_TIERS);
      return;
    }

    const tiers: FlashCardTierRequest[] = rows.map((row) => ({
      cardCount: Number(row.cardCount),
      stars: Number(row.stars),
      ...(row.id ? { id: row.id } : {}),
    }));

    const ok = await this.action.run(
      set.id,
      async () => {
        await this.flashCards.enableSet(childId, set.id, tiers);
        this.data.reload();
      },
      SAVE_ERROR,
    );

    if (ok) {
      this.savedSetId.set(set.id);
    }
  }

  protected async disable(set: FlashCardSet): Promise<void> {
    const childId = this.childId();

    if (!childId) {
      return;
    }

    this.savedSetId.set(null);
    await this.action.run(
      set.id,
      async () => {
        await this.flashCards.disableSet(childId, set.id);
        this.data.reload();
      },
      SAVE_ERROR,
    );
  }
}

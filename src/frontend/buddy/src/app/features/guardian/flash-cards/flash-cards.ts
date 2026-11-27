import { Component, computed, inject, linkedSignal, resource, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';

import { FlashCardSet, FlashCardsService } from '../../../core/flash-cards.service';
import { GuardiansService } from '../../../core/guardians.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';
import { Page } from '../../../shared/page/page';
import { SegmentedControl } from '../../../shared/segmented-control/segmented-control';
import { FlashCardChild } from './flash-card-child/flash-card-child';
import { FlashCardLibrary } from './flash-card-library/flash-card-library';

export type FlashCardsTab = 'library' | 'child';

// The guardian's flash cards: the library of sets (maths rules and text cards), and per child
// which sets they play, in how many cards and for how many stars, plus their finished games.
// ?child=<id> opens the per-child tab on that child.
@Component({
  selector: 'app-guardian-flash-cards',
  imports: [FlashCardChild, FlashCardLibrary, Page, SegmentedControl, TranslatePipe],
  templateUrl: './flash-cards.html',
})
export class GuardianFlashCards {
  private readonly flashCards = inject(FlashCardsService);
  private readonly guardians = inject(GuardiansService);
  private readonly translation = inject(TranslationService);
  private readonly requestedChildId =
    inject(ActivatedRoute, { optional: true })?.snapshot.queryParamMap.get('child') ?? undefined;

  protected readonly tab = signal<FlashCardsTab>(this.requestedChildId ? 'child' : 'library');
  protected readonly tabOptions = computed(() => [
    {
      value: 'library' as const,
      label: this.translation.translate('flashCards.guardian.tabs.library'),
    },
    {
      value: 'child' as const,
      label: this.translation.translate('flashCards.guardian.tabs.child'),
    },
  ]);

  private readonly loaded = resource({
    loader: async () => ({
      sets: await this.flashCards.listSets(),
      children: await this.guardians.listMyChildren(),
    }),
  });
  // Edits in the library change this copy, so the per-child tab sees them without a reload.
  protected readonly sets = linkedSignal<FlashCardSet[]>(() =>
    this.loaded.hasValue() ? this.loaded.value().sets : [],
  );
  protected readonly children = computed(() =>
    this.loaded.hasValue() ? this.loaded.value().children : [],
  );
  protected readonly selectedChildId = linkedSignal(() => {
    const children = this.children();
    return children.find((child) => child.id === this.requestedChildId)?.id ?? children[0]?.id;
  });
  protected readonly selectedChild = computed(() =>
    this.children().find((child) => child.id === this.selectedChildId()),
  );
  protected readonly loading = computed(() => this.loaded.isLoading());
  protected readonly failed = computed(() => this.loaded.error() !== undefined);

  protected onSetSaved(saved: FlashCardSet): void {
    this.sets.update((sets) =>
      sets.some((set) => set.id === saved.id)
        ? sets.map((set) => (set.id === saved.id ? saved : set))
        : [...sets, saved],
    );
  }

  protected onSetDeleted(setId: string): void {
    this.sets.update((sets) => sets.filter((set) => set.id !== setId));
  }
}

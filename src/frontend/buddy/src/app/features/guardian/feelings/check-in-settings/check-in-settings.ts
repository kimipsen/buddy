import { Component, computed, inject, input, linkedSignal, output } from '@angular/core';

import { FeelingsDisplayMode, FeelingsService } from '../../../../core/feelings.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { createAction } from '../../../../shared/action-state/action-state';
import { Card } from '../../../../shared/card/card';
import { RepeatableRow } from '../../../../shared/repeatable-row/repeatable-row';
import { TimeSelect } from '../../../../shared/time-select/time-select';
import { Toggle } from '../../../../shared/toggle/toggle';

export const MAX_PROMPT_TIMES = 4;

/**
 * The child's two check-in settings (Questions 6 and 9): up to four reminder times, and faces-only
 * buttons for a child who doesn't read yet. Both are stored on the child's feelings journal.
 */
@Component({
  selector: 'app-feelings-check-in-settings',
  imports: [Card, RepeatableRow, TimeSelect, Toggle, TranslatePipe],
  templateUrl: './check-in-settings.html',
})
export class FeelingsCheckInSettings {
  private readonly feelings = inject(FeelingsService);

  readonly childId = input.required<string>();
  readonly childName = input.required<string>();
  readonly promptTimes = input.required<readonly string[]>();
  readonly displayMode = input.required<FeelingsDisplayMode>();
  readonly locked = input(false);

  readonly promptTimesSaved = output<string[]>();
  readonly displayModeSaved = output<FeelingsDisplayMode>();

  // The rows being edited, refilled whenever the saved times (or the child) change.
  protected readonly rows = linkedSignal(() => [...this.promptTimes()]);
  protected readonly savedNotice = linkedSignal({ source: this.childId, computation: () => false });
  protected readonly saving = createAction();
  protected readonly switching = createAction();
  protected readonly maxTimes = MAX_PROMPT_TIMES;

  protected readonly facesOnly = computed(() => this.displayMode() === 'FacesOnly');
  protected readonly canSave = computed(
    () => !this.locked() && !this.saving.busy() && this.rows().every((time) => time !== ''),
  );

  protected setRow(index: number, time: string): void {
    this.rows.update((rows) => rows.map((row, i) => (i === index ? time : row)));
    this.savedNotice.set(false);
  }

  protected addRow(): void {
    this.rows.update((rows) => (rows.length < MAX_PROMPT_TIMES ? [...rows, ''] : rows));
    this.savedNotice.set(false);
  }

  protected removeRow(index: number): void {
    this.rows.update((rows) => rows.filter((_, i) => i !== index));
    this.savedNotice.set(false);
  }

  protected async savePromptTimes(): Promise<void> {
    const times = [...new Set(this.rows())];

    await this.saving.run(
      true,
      async () => {
        const saved = await this.feelings.setPromptTimes(this.childId(), times);
        this.promptTimesSaved.emit(saved);
        this.savedNotice.set(true);
      },
      'feelings.guardian.settings.promptsError',
    );
  }

  protected async setFacesOnly(facesOnly: boolean): Promise<void> {
    const mode: FeelingsDisplayMode = facesOnly ? 'FacesOnly' : 'FacesAndWords';

    await this.switching.run(
      true,
      async () => {
        this.displayModeSaved.emit(await this.feelings.setDisplayMode(this.childId(), mode));
      },
      'feelings.guardian.settings.facesOnlyError',
    );
  }
}

import { Component, computed, inject, input, linkedSignal, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { SleepDiaryService } from '../../../../core/sleep-diary.service';
import { createAction } from '../../../../shared/action-state/action-state';

// The diary-wide "sleep hygiene measures" box -- kept apart from the nightly form so it reads as a
// setting, not a log row (visual-specification.md, worked example).
@Component({
  selector: 'app-hygiene-notes',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './hygiene-notes.html',
})
export class HygieneNotes {
  private readonly sleepDiary = inject(SleepDiaryService);

  readonly childId = input.required<string>();
  readonly notes = input<string>('');
  // True while the page is still loading the stored notes, so a save can't overwrite them unseen.
  readonly locked = input(false);

  readonly saved = output<string>();

  protected readonly text = linkedSignal(() => this.notes());
  protected readonly changed = computed(() => this.text().trim() !== this.notes());
  protected readonly saving = createAction();
  private readonly savedFor = signal<string | null>(null);
  protected readonly justSaved = computed(
    () => this.savedFor() === this.childId() && !this.changed(),
  );

  protected async save(): Promise<void> {
    const childId = this.childId();
    const notes = this.text().trim();

    await this.saving.run(
      true,
      async () => {
        await this.sleepDiary.updateHygieneNotes(childId, notes);
        this.saved.emit(notes);
        this.savedFor.set(childId);
      },
      'sleepDiary.hygiene.saveError',
    );
  }
}

import {
  Component,
  ElementRef,
  HostListener,
  Injector,
  OnInit,
  afterNextRender,
  computed,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';

import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { Night, NightEntry, nightEntry, scheduledEntry } from '../../../../core/night-duty.service';
import {
  NOBODY_CHOICE,
  NightPeople,
  SLEEPOVER_CHOICE,
  babysitterChoice,
  choiceOf,
  isCompleteEntry,
  responsibleFromChoice,
  responsibleText,
  sleepoverOf,
} from '../night-people';
import { keepTabInside } from '../../../../shared/focus-trap/focus-trap';

// The <select> value for "nothing planned this night" (a cleared night).
export const CLEARED_CHOICE = 'cleared';

// Changes one night: who is on duty (a guardian, a babysitter, a sleepover, nobody with a note, or
// nothing planned) and the note. The note starts from the night's current entry -- or the
// schedule's, so changing only the person on a screen night keeps "Screen night"
// (docs/backend/analysis/night-duty.md, Question 5). The parent owns saving.
@Component({
  selector: 'app-night-edit-dialog',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './night-edit-dialog.html',
})
export class NightEditDialog implements OnInit {
  private readonly injector = inject(Injector);
  private readonly translation = inject(TranslationService);

  readonly night = input.required<Night>();
  readonly dayLabel = input.required<string>();
  readonly people = input.required<NightPeople>();
  readonly busy = input(false);
  // An i18n key shown above the buttons.
  readonly error = input<string | null>(null);

  // null = nothing planned this night.
  readonly save = output<NightEntry | null>();
  readonly backToSchedule = output<void>();
  readonly closed = output<void>();

  private readonly panel = viewChild<ElementRef<HTMLElement>>('panel');
  private readonly whoSelect = viewChild<ElementRef<HTMLSelectElement>>('whoSelect');

  protected readonly cleared = CLEARED_CHOICE;
  protected readonly nobody = NOBODY_CHOICE;
  protected readonly sleepoverChoice = SLEEPOVER_CHOICE;
  protected readonly babysitterChoice = babysitterChoice;

  protected readonly choice = signal<string>(NOBODY_CHOICE);
  protected readonly hostName = signal('');
  protected readonly location = signal('');
  // Not shown, but kept so a save doesn't wipe a stored contact.
  private contactInfo = '';
  protected readonly note = signal('');
  protected readonly incomplete = signal(false);

  protected readonly isOverride = computed(() => {
    const status = this.night().status;
    return status.kind === 1 || (status.kind === 2 && status.source === 'Override');
  });

  // "Fixed schedule: Dad." -- what the night falls back to.
  protected readonly scheduleText = computed((): string | null => {
    const night = this.night();
    const scheduled =
      night.status.kind === 2 && night.status.source === 'Pattern'
        ? night.status.entry
        : scheduledEntry(night);
    if (!scheduled) {
      return null;
    }
    const who = responsibleText(scheduled.responsible, this.people(), {
      guardian: this.translation.translate('nightDuty.who.guardian'),
      babysitter: this.translation.translate('nightDuty.who.babysitter'),
      sleepover: (host) => this.translation.translate('nightDuty.who.sleepover', { host }),
    });
    return [who?.text, scheduled.note].filter(Boolean).join(' · ');
  });

  protected readonly titleId = 'night-edit-title';

  ngOnInit(): void {
    const night = this.night();
    const entry = nightEntry(night);

    if (entry) {
      this.choice.set(choiceOf(entry.responsible));
      const sleepover = sleepoverOf(entry.responsible);
      this.hostName.set(sleepover.hostName);
      this.location.set(sleepover.location);
      this.contactInfo = sleepover.contactInfo;
      this.note.set(entry.note ?? '');
    } else if (night.status.kind === 1) {
      this.choice.set(CLEARED_CHOICE);
    } else {
      this.note.set(scheduledEntry(night)?.note ?? '');
    }

    afterNextRender(() => this.whoSelect()?.nativeElement.focus(), { injector: this.injector });
  }

  protected setChoice(choice: string): void {
    this.choice.set(choice);
    this.incomplete.set(false);
  }

  protected submit(): void {
    if (this.busy()) {
      return;
    }
    if (this.choice() === CLEARED_CHOICE) {
      this.save.emit(null);
      return;
    }

    const entry: NightEntry = {
      responsible: responsibleFromChoice(this.choice(), {
        hostName: this.hostName(),
        location: this.location(),
        contactInfo: this.contactInfo,
      }),
      note: this.note().trim(),
    };

    if (!isCompleteEntry(entry)) {
      this.incomplete.set(true);
      return;
    }
    this.save.emit(entry);
  }

  protected close(): void {
    if (!this.busy()) {
      this.closed.emit();
    }
  }

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    this.close();
  }

  // aria-modal promises the page behind is inert, so keep Tab / Shift+Tab inside the dialog.
  @HostListener('document:keydown.tab', ['$event'])
  @HostListener('document:keydown.shift.tab', ['$event'])
  protected onTab(event: Event): void {
    keepTabInside(this.panel()?.nativeElement, event);
  }
}

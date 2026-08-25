import { Pipe, PipeTransform, inject } from '@angular/core';

import { TranslationService } from './i18n/translation.service';

// Pickups and medicine doses store plain "HH:mm[:ss]" wall-clock strings, with no date or time
// zone attached, so UserDatePipe (which needs a real Date and a time zone) doesn't fit. This
// builds a throwaway Date from just the hour/minute so Intl.DateTimeFormat can render it in the
// signed-in user's selected language (e.g. 24-hour "14:00" for da vs 12-hour "2:00 PM" for en).
@Pipe({ name: 'timeOfDay', pure: false })
export class TimeOfDayPipe implements PipeTransform {
  private readonly translation = inject(TranslationService);

  transform(value: string | null | undefined): string {
    if (!value) {
      return '';
    }

    const [hours, minutes] = value.split(':').map(Number);
    const date = new Date(2000, 0, 1, hours, minutes);

    return new Intl.DateTimeFormat(this.translation.language(), { hour: 'numeric', minute: '2-digit' }).format(date);
  }
}

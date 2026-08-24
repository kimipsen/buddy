import { Pipe, PipeTransform, inject } from '@angular/core';

import { TranslationService } from './i18n/translation.service';
import { UsersService } from './users.service';

export type UserDateFormat = 'medium' | 'shortTime';

// Angular's built-in DatePipe only understands fixed UTC-offset strings (e.g. "+0100") for its
// `timezone` parameter, not IANA zone names -- passing "Europe/Copenhagen" to it silently falls
// back to the browser's local time instead of erroring. Intl.DateTimeFormat handles IANA zones
// correctly, so this pipe uses that directly instead of wrapping DatePipe.
@Pipe({ name: 'userDate', pure: false })
export class UserDatePipe implements PipeTransform {
  private readonly users = inject(UsersService);
  private readonly translation = inject(TranslationService);

  transform(value: string | number | Date | null | undefined, format: UserDateFormat = 'medium'): string {
    if (value === null || value === undefined || value === '') {
      return '';
    }

    const date = value instanceof Date ? value : new Date(value);
    const timeZone = this.users.timeZoneId();

    const options: Intl.DateTimeFormatOptions =
      format === 'shortTime' ? { timeStyle: 'short', timeZone } : { dateStyle: 'medium', timeStyle: 'medium', timeZone };

    return new Intl.DateTimeFormat(this.translation.language(), options).format(date);
  }
}

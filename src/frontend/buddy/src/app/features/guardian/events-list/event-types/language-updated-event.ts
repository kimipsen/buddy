import { Component, input } from '@angular/core';

import { LANGUAGE_NAMES, isSupportedLanguage } from '../../../../core/i18n/language';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { UserDatePipe } from '../../../../core/user-date.pipe';
import { LanguageUpdatedData } from './user-event.model';

@Component({
  selector: 'app-language-updated-event',
  imports: [UserDatePipe, TranslatePipe],
  templateUrl: './language-updated-event.html',
})
export class LanguageUpdatedEvent {
  readonly data = input.required<LanguageUpdatedData>();

  protected languageName(code: string): string {
    return isSupportedLanguage(code) ? LANGUAGE_NAMES[code] : code;
  }
}

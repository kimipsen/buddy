import { Pipe, PipeTransform, inject } from '@angular/core';

import { TranslationService } from './translation.service';

// Impure because the translated string must be recomputed when the current language changes,
// even though the key argument itself doesn't -- Angular re-invokes an impure pipe every change
// detection cycle rather than only when its arguments change by reference.
@Pipe({ name: 'translate', pure: false })
export class TranslatePipe implements PipeTransform {
  private readonly i18n = inject(TranslationService);

  transform(key: string, params?: Record<string, string | number>): string {
    return this.i18n.translate(key, params);
  }
}

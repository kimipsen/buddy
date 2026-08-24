import { Component, input } from '@angular/core';

import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { UserDatePipe } from '../../../../core/user-date.pipe';
import { NameUpdatedData } from './user-event.model';

@Component({
  selector: 'app-name-updated-event',
  imports: [UserDatePipe, TranslatePipe],
  templateUrl: './name-updated-event.html'
})
export class NameUpdatedEvent {
  readonly data = input.required<NameUpdatedData>();
}

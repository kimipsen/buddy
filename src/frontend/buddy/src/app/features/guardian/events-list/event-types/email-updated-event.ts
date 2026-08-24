import { Component, input } from '@angular/core';

import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { UserDatePipe } from '../../../../core/user-date.pipe';
import { EmailUpdatedData } from './user-event.model';

@Component({
  selector: 'app-email-updated-event',
  imports: [UserDatePipe, TranslatePipe],
  templateUrl: './email-updated-event.html'
})
export class EmailUpdatedEvent {
  readonly data = input.required<EmailUpdatedData>();
}

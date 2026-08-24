import { Component, input } from '@angular/core';

import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { UserDatePipe } from '../../../../core/user-date.pipe';
import { EmailVerificationRequestedData } from './user-event.model';

@Component({
  selector: 'app-email-verification-requested-event',
  imports: [UserDatePipe, TranslatePipe],
  templateUrl: './email-verification-requested-event.html'
})
export class EmailVerificationRequestedEvent {
  readonly data = input.required<EmailVerificationRequestedData>();
}

import { Component, input } from '@angular/core';

import { UserDatePipe } from '../../../../core/user-date.pipe';
import { EmailVerificationRequestedData } from './user-event.model';

@Component({
  selector: 'app-email-verification-requested-event',
  imports: [UserDatePipe],
  templateUrl: './email-verification-requested-event.html'
})
export class EmailVerificationRequestedEvent {
  readonly data = input.required<EmailVerificationRequestedData>();
}

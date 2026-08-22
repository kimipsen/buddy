import { DatePipe } from '@angular/common';
import { Component, input } from '@angular/core';

import { EmailVerificationRequestedData } from './user-event.model';

@Component({
  selector: 'app-email-verification-requested-event',
  imports: [DatePipe],
  templateUrl: './email-verification-requested-event.html'
})
export class EmailVerificationRequestedEvent {
  readonly data = input.required<EmailVerificationRequestedData>();
}

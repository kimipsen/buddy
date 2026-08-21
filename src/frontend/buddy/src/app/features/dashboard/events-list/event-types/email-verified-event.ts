import { DatePipe } from '@angular/common';
import { Component, input } from '@angular/core';

import { EmailVerifiedData } from './user-event.model';

@Component({
  selector: 'app-email-verified-event',
  imports: [DatePipe],
  templateUrl: './email-verified-event.html'
})
export class EmailVerifiedEvent {
  readonly data = input.required<EmailVerifiedData>();
}

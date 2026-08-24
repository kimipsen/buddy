import { Component, input } from '@angular/core';

import { UserDatePipe } from '../../../../core/user-date.pipe';
import { EmailVerifiedData } from './user-event.model';

@Component({
  selector: 'app-email-verified-event',
  imports: [UserDatePipe],
  templateUrl: './email-verified-event.html'
})
export class EmailVerifiedEvent {
  readonly data = input.required<EmailVerifiedData>();
}

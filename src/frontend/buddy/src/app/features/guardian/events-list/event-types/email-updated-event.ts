import { DatePipe } from '@angular/common';
import { Component, input } from '@angular/core';

import { EmailUpdatedData } from './user-event.model';

@Component({
  selector: 'app-email-updated-event',
  imports: [DatePipe],
  templateUrl: './email-updated-event.html'
})
export class EmailUpdatedEvent {
  readonly data = input.required<EmailUpdatedData>();
}

import { DatePipe } from '@angular/common';
import { Component, input } from '@angular/core';

import { NameUpdatedData } from './user-event.model';

@Component({
  selector: 'app-name-updated-event',
  imports: [DatePipe],
  templateUrl: './name-updated-event.html'
})
export class NameUpdatedEvent {
  readonly data = input.required<NameUpdatedData>();
}

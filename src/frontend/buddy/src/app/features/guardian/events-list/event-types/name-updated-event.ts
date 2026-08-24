import { Component, input } from '@angular/core';

import { UserDatePipe } from '../../../../core/user-date.pipe';
import { NameUpdatedData } from './user-event.model';

@Component({
  selector: 'app-name-updated-event',
  imports: [UserDatePipe],
  templateUrl: './name-updated-event.html'
})
export class NameUpdatedEvent {
  readonly data = input.required<NameUpdatedData>();
}

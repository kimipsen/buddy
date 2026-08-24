import { Component, input } from '@angular/core';

import { UserDatePipe } from '../../../../core/user-date.pipe';
import { UserCreatedData } from './user-event.model';

@Component({
  selector: 'app-user-created-event',
  imports: [UserDatePipe],
  templateUrl: './user-created-event.html'
})
export class UserCreatedEvent {
  readonly data = input.required<UserCreatedData>();
}

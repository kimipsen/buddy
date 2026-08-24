import { Component, input } from '@angular/core';

import { UserDatePipe } from '../../../../core/user-date.pipe';
import { UserDeletedData } from './user-event.model';

@Component({
  selector: 'app-user-deleted-event',
  imports: [UserDatePipe],
  templateUrl: './user-deleted-event.html'
})
export class UserDeletedEvent {
  readonly data = input.required<UserDeletedData>();
}

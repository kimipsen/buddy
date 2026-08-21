import { DatePipe } from '@angular/common';
import { Component, input } from '@angular/core';

import { UserDeletedData } from './user-event.model';

@Component({
  selector: 'app-user-deleted-event',
  imports: [DatePipe],
  templateUrl: './user-deleted-event.html'
})
export class UserDeletedEvent {
  readonly data = input.required<UserDeletedData>();
}

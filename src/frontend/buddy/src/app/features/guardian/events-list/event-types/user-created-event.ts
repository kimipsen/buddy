import { DatePipe } from '@angular/common';
import { Component, input } from '@angular/core';

import { UserCreatedData } from './user-event.model';

@Component({
  selector: 'app-user-created-event',
  imports: [DatePipe],
  templateUrl: './user-created-event.html'
})
export class UserCreatedEvent {
  readonly data = input.required<UserCreatedData>();
}

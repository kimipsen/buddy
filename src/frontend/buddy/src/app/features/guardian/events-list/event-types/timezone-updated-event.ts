import { Component, input } from '@angular/core';

import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { UserDatePipe } from '../../../../core/user-date.pipe';
import { TimeZoneUpdatedData } from './user-event.model';

@Component({
  selector: 'app-timezone-updated-event',
  imports: [UserDatePipe, TranslatePipe],
  templateUrl: './timezone-updated-event.html'
})
export class TimeZoneUpdatedEvent {
  readonly data = input.required<TimeZoneUpdatedData>();
}

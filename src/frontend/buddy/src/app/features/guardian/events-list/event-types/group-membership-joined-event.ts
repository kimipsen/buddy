import { Component, input } from '@angular/core';

import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { UserDatePipe } from '../../../../core/user-date.pipe';
import { GroupMembershipJoinedData } from './user-event.model';

@Component({
  selector: 'app-group-membership-joined-event',
  imports: [UserDatePipe, TranslatePipe],
  templateUrl: './group-membership-joined-event.html'
})
export class GroupMembershipJoinedEvent {
  readonly data = input.required<GroupMembershipJoinedData>();
}

import { Component, input } from '@angular/core';

import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { UserDatePipe } from '../../../../core/user-date.pipe';
import { GroupInvitationSentData } from './user-event.model';

@Component({
  selector: 'app-group-invitation-sent-event',
  imports: [UserDatePipe, TranslatePipe],
  templateUrl: './group-invitation-sent-event.html'
})
export class GroupInvitationSentEvent {
  readonly data = input.required<GroupInvitationSentData>();
}

import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { EventsList } from '../events-list/events-list';
import { DeleteAccount } from './delete-account/delete-account';
import { ManageCalendars } from './manage-calendars/manage-calendars';
import { ManageChildren } from './manage-children/manage-children';
import { ManageGroups } from './manage-groups/manage-groups';
import { MyProfile } from './my-profile/my-profile';

@Component({
  selector: 'app-guardian-admin',
  imports: [RouterLink, TranslatePipe, MyProfile, ManageChildren, ManageCalendars, ManageGroups, EventsList, DeleteAccount],
  templateUrl: './admin.html'
})
export class GuardianAdmin {}

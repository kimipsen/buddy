import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';

import { FeaturesService } from '../../../core/features.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { AiProviderSettingsComponent } from './ai-provider-settings/ai-provider-settings';
import { DeleteAccount } from './delete-account/delete-account';
import { DownloadMyData } from './download-my-data/download-my-data';
import { ManageCalendars } from './manage-calendars/manage-calendars';
import { ManageChildren } from './manage-children/manage-children';
import { ManageGroups } from './manage-groups/manage-groups';
import { MyProfile } from './my-profile/my-profile';

@Component({
  selector: 'app-guardian-admin',
  imports: [
    RouterLink,
    TranslatePipe,
    MyProfile,
    ManageChildren,
    ManageCalendars,
    ManageGroups,
    AiProviderSettingsComponent,
    DownloadMyData,
    DeleteAccount,
  ],
  templateUrl: './admin.html',
})
export class GuardianAdmin {
  protected readonly features = inject(FeaturesService);
}

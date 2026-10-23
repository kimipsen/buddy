import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';

import { FeaturesService } from '../../../core/features.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { ManagePickups } from './manage-pickups/manage-pickups';
import { PickupIcal } from './pickup-ical/pickup-ical';
import { Page } from '../../../shared/page/page';

@Component({
  selector: 'app-guardian-pickup',
  imports: [RouterLink, ManagePickups, PickupIcal, TranslatePipe, Page],
  templateUrl: './pickup.html',
})
export class GuardianPickup {
  protected readonly features = inject(FeaturesService);
}

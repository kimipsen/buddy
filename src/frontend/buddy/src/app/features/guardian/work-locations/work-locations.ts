import { Component, inject, resource } from '@angular/core';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { UsersService } from '../../../core/users.service';
import { WorkLocationsService } from '../../../core/work-locations.service';
import { LoadingSpinner } from '../../../shared/loading-spinner/loading-spinner';
import { ManageWorkLocations } from './manage-work-locations/manage-work-locations';
import { WorkDayOverrides } from './work-day-overrides/work-day-overrides';
import { WorkPatternEditor } from './work-pattern-editor/work-pattern-editor';
import { Page } from '../../../shared/page/page';

// The guardian's own work locations, pattern and exceptions (see
// docs/backend/analysis/work-locations.md). Each section saves on its own and asks this page to
// reload the schedule afterwards, so locations, pattern and days never drift apart. A reload keeps
// the sections on screen (the previous schedule stays the value until the new one arrives).
@Component({
  selector: 'app-guardian-work-locations',
  imports: [
    TranslatePipe,
    LoadingSpinner,
    ManageWorkLocations,
    WorkPatternEditor,
    WorkDayOverrides,
    Page,
  ],
  templateUrl: './work-locations.html',
})
export class GuardianWorkLocations {
  private readonly users = inject(UsersService);
  private readonly workLocations = inject(WorkLocationsService);

  protected readonly schedule = resource({
    loader: async () => {
      const me = await this.users.ensureCurrentUser();
      return this.workLocations.getSchedule(me.id);
    },
  });

  protected reload(): void {
    this.schedule.reload();
  }
}

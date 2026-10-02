import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { UsersService } from '../../../core/users.service';
import { WorkLocationSchedule, WorkLocationsService } from '../../../core/work-locations.service';
import { LoadingSpinner } from '../../../shared/loading-spinner/loading-spinner';
import { ManageWorkLocations } from './manage-work-locations/manage-work-locations';
import { WorkDayOverrides } from './work-day-overrides/work-day-overrides';
import { WorkPatternEditor } from './work-pattern-editor/work-pattern-editor';

// The guardian's own work locations, pattern and exceptions (see
// docs/backend/analysis/work-locations.md). Each section saves on its own and asks this page to
// reload the schedule afterwards, so locations, pattern and days never drift apart.
@Component({
  selector: 'app-guardian-work-locations',
  imports: [
    RouterLink,
    TranslatePipe,
    LoadingSpinner,
    ManageWorkLocations,
    WorkPatternEditor,
    WorkDayOverrides,
  ],
  templateUrl: './work-locations.html',
})
export class GuardianWorkLocations implements OnInit {
  private readonly users = inject(UsersService);
  private readonly workLocations = inject(WorkLocationsService);

  protected readonly schedule = signal<WorkLocationSchedule | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.error.set(null);

    try {
      const me = await this.users.ensureCurrentUser();
      this.schedule.set(await this.workLocations.getSchedule(me.id));
    } catch {
      this.error.set('workLocations.loadError');
    } finally {
      this.loading.set(false);
    }
  }
}

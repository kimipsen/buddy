import { Component, computed, inject, linkedSignal, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { BabysittersService, ChildBabysitter } from '../../../core/babysitters.service';
import { FeaturesService } from '../../../core/features.service';
import { ChildSummary, GuardiansService } from '../../../core/guardians.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { NightDutyService } from '../../../core/night-duty.service';
import { LoadingSpinner } from '../../../shared/loading-spinner/loading-spinner';
import { Page } from '../../../shared/page/page';
import { NightIcal } from './night-ical/night-ical';
import { NightPatternEditor } from './night-pattern-editor/night-pattern-editor';
import { NightPeople } from './night-people';
import { NightWeek } from './night-week/night-week';

// Who is on duty at night for each child (docs/backend/analysis/night-duty.md): this week's
// resolved nights, where single nights are changed or switched, and the fixed schedule they come
// from. Saving the schedule bumps `revision`, so the week reloads against the new version.
@Component({
  selector: 'app-guardian-night-duty',
  imports: [
    FormsModule,
    RouterLink,
    TranslatePipe,
    LoadingSpinner,
    Page,
    NightWeek,
    NightPatternEditor,
    NightIcal,
  ],
  templateUrl: './night-duty.html',
})
export class GuardianNightDuty {
  protected readonly features = inject(FeaturesService);
  private readonly guardians = inject(GuardiansService);
  private readonly babysitters = inject(BabysittersService);
  private readonly nightDuty = inject(NightDutyService);

  protected readonly children = resource({ loader: () => this.guardians.listMyChildren() });
  protected readonly childList = computed((): ChildSummary[] =>
    this.children.hasValue() ? this.children.value() : [],
  );
  // The first child until the guardian picks another.
  protected readonly selectedChildId = linkedSignal(() => this.childList()[0]?.id);
  protected readonly selectedChild = computed(() =>
    this.childList().find((child) => child.id === this.selectedChildId()),
  );

  protected readonly revision = signal(0);

  // The child's guardians and their babysitters, and the fixed schedule, for the selected child.
  protected readonly data = resource({
    params: () => this.selectedChildId(),
    loader: async ({ params: childId }) => {
      const [guardians, babysitters, schedule] = await Promise.all([
        this.guardians.listChildGuardians(childId),
        // Best effort: without babysitters only the babysitter choices are missing. With the
        // feature off the route isn't mapped, so there is nothing to ask for.
        this.features.enabled('babysitters')
          ? this.babysitters.listForChild(childId).catch((): ChildBabysitter[] => [])
          : Promise.resolve([]),
        this.nightDuty.getSchedule(childId),
      ]);
      const people: NightPeople = { guardians, babysitters };
      return { people, schedule };
    },
  });

  protected scheduleSaved(): void {
    this.data.reload();
    this.revision.update((value) => value + 1);
  }
}

import { Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

import { FeatureName, FeaturesService } from '../../../../core/features.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

type TabIcon = 'today' | 'calendar' | 'meals' | 'medicine';

interface Tab {
  readonly link: string;
  readonly label: string;
  readonly icon: TabIcon;
  // Today is the dashboard at /guardian, which every other guardian URL starts with.
  readonly exact: boolean;
  readonly feature?: FeatureName;
}

const TABS: readonly Tab[] = [
  { link: '/guardian', label: 'shell.tabs.today', icon: 'today', exact: true },
  { link: '/guardian/calendar', label: 'shell.tabs.calendar', icon: 'calendar', exact: false },
  {
    link: '/guardian/mealplan',
    label: 'shell.tabs.meals',
    icon: 'meals',
    exact: false,
    feature: 'mealplans',
  },
  {
    link: '/guardian/medicine',
    label: 'shell.tabs.medicine',
    icon: 'medicine',
    exact: false,
    feature: 'medicines',
  },
];

// The guardian's phone navigation (docs/frontend/analysis/responsive-layout.md, Phase 5): the four
// most used pages at the bottom of the screen. The account menu keeps every link, on every size.
@Component({
  selector: 'app-tab-bar',
  imports: [RouterLink, RouterLinkActive, TranslatePipe],
  templateUrl: './tab-bar.html',
})
export class TabBar {
  private readonly features = inject(FeaturesService);

  // Tabs for a feature this installation has turned off are dropped, like their menu links.
  protected readonly tabs = computed(() =>
    TABS.filter((tab) => tab.feature === undefined || this.features.enabled(tab.feature)),
  );
}

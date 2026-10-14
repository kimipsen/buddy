import { Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

import { FeaturesService } from '../../../../core/features.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { visibleNavGroups } from '../nav-items';

// The guardian's phone navigation (docs/frontend/analysis/responsive-layout.md, Phase 5): the four
// most used pages at the bottom of the screen, the nav-items.ts pages with a `tab`. The page menu
// keeps every link, on every size.
@Component({
  selector: 'app-tab-bar',
  imports: [RouterLink, RouterLinkActive, TranslatePipe],
  templateUrl: './tab-bar.html',
})
export class TabBar {
  private readonly features = inject(FeaturesService);

  // Tabs for a feature this installation has turned off are dropped, like their menu links.
  protected readonly tabs = computed(() =>
    visibleNavGroups(this.features).flatMap((group) =>
      group.items.flatMap((item) =>
        item.tab ? [{ link: item.link, exact: !!item.exact, ...item.tab }] : [],
      ),
    ),
  );
}

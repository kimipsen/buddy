import { Component, computed, HostListener, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

import { FeaturesService } from '../../../../core/features.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { visibleNavGroups } from '../nav-items';

// Form controls that bring up the on-screen keyboard (or the iOS picker wheel for a select).
const TYPING_FIELD =
  'textarea, select, input:not([type="checkbox"], [type="radio"], [type="range"], [type="color"], [type="file"], [type="button"], [type="submit"], [type="reset"], [type="image"])';

function isTypingField(target: EventTarget | null): boolean {
  return target instanceof Element && target.matches(TYPING_FIELD);
}

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

  // While a field has focus, a touch screen shows its keyboard. iOS Safari doesn't shrink the
  // layout viewport for it, so a fixed bar ends up floating over the page or the keyboard
  // (issue #84); the template hides the bar on touch screens until the field loses focus.
  protected readonly typing = signal(false);

  @HostListener('document:focusin', ['$event'])
  protected onFocusIn(event: FocusEvent): void {
    this.typing.set(isTypingField(event.target));
  }

  // relatedTarget is where focus goes next: moving from one field to the next keeps the bar away.
  @HostListener('document:focusout', ['$event'])
  protected onFocusOut(event: FocusEvent): void {
    this.typing.set(isTypingField(event.relatedTarget));
  }
}

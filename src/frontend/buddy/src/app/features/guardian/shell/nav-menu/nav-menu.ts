import {
  Component,
  ElementRef,
  HostListener,
  Injector,
  afterNextRender,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

import { FeaturesService } from '../../../../core/features.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { visibleNavGroups } from '../nav-items';

// The guardian's page menu: every page, grouped by what it is for (nav-items.ts). Account items
// (theme, settings, help, sign out) are in the profile menu next to it.
@Component({
  selector: 'app-nav-menu',
  imports: [RouterLink, RouterLinkActive, TranslatePipe],
  templateUrl: './nav-menu.html',
})
export class NavMenu {
  private readonly features = inject(FeaturesService);

  private readonly injector = inject(Injector);
  private readonly trigger = viewChild.required<ElementRef<HTMLButtonElement>>('trigger');
  private readonly panel = viewChild<ElementRef<HTMLElement>>('panel');

  protected readonly groups = computed(() => visibleNavGroups(this.features));
  protected readonly open = signal(false);

  protected toggle(): void {
    this.open.update((value) => !value);

    if (this.open()) {
      afterNextRender(() => this.panel()?.nativeElement.querySelector('a')?.focus(), {
        injector: this.injector,
      });
    }
  }

  protected close(): void {
    this.open.set(false);
  }

  // Backdrop click / Escape: close and hand focus back to the toggle, since the focused link
  // inside the panel is about to be removed from the DOM.
  protected dismiss(): void {
    this.close();
    this.trigger().nativeElement.focus();
  }

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    if (this.open()) {
      this.dismiss();
    }
  }
}

import { Component, ElementRef, HostListener, inject, signal, viewChild } from '@angular/core';
import { ActivatedRoute, RouterLink, RouterOutlet } from '@angular/router';

import { FeaturesService } from '../../../core/features.service';
import { HelpTopic } from '../../../core/help/help-topic';
import { findHelpTopic } from '../../../core/help/help-topics';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { HelpPanel } from './help-panel/help-panel';
import { ProfileMenu } from './profile-menu/profile-menu';

@Component({
  selector: 'app-guardian-shell',
  imports: [RouterOutlet, RouterLink, ProfileMenu, TranslatePipe, HelpPanel],
  templateUrl: './guardian-shell.html',
})
export class GuardianShell {
  private readonly features = inject(FeaturesService);

  private readonly route = inject(ActivatedRoute);
  private readonly helpTrigger = viewChild<ElementRef<HTMLButtonElement>>('helpTrigger');
  private readonly helpRegion = viewChild<ElementRef<HTMLElement>>('helpRegion');

  protected readonly helpPanelId = 'page-help';
  // The current page's topic, from its route's `data.helpTopic`. Pages without one get no button.
  protected readonly helpTopic = signal<HelpTopic | undefined>(undefined);
  protected readonly helpOpen = signal(false);

  // Runs whenever the outlet shows another page: pick up its topic and fold away the previous
  // page's help.
  protected onActivate(): void {
    let page = this.route.firstChild;
    while (page?.firstChild) {
      page = page.firstChild;
    }

    this.helpOpen.set(false);
    // No ? button at all while help is turned off for this installation.
    this.helpTopic.set(
      this.features.enabled('help') ? findHelpTopic(page?.snapshot.data['helpTopic']) : undefined,
    );
  }

  protected toggleHelp(): void {
    this.helpOpen.update((open) => !open);
  }

  // Close and hand focus back to the toggle, since a focused control inside the panel is about to
  // be removed from the DOM.
  protected closeHelp(): void {
    this.helpOpen.set(false);
    this.helpTrigger()?.nativeElement.focus();
  }

  // Only while focus is on the help button or inside the panel: Escape elsewhere belongs to
  // whatever has focus there (the profile menu, a page's own dialog).
  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    const focused = document.activeElement;
    const inHelp =
      focused === this.helpTrigger()?.nativeElement ||
      !!this.helpRegion()?.nativeElement.contains(focused);

    if (this.helpOpen() && inHelp) {
      this.closeHelp();
    }
  }
}

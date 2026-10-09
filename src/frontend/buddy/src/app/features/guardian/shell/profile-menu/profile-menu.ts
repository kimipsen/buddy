import {
  Component,
  ElementRef,
  HostListener,
  Injector,
  afterNextRender,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../../../core/auth.service';
import { FeaturesService } from '../../../../core/features.service';
import { RuntimeConfigService } from '../../../../core/runtime-config.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { THEME_MODES, ThemeMode } from '../../../../core/theme';
import { ThemeService } from '../../../../core/theme.service';

@Component({
  selector: 'app-profile-menu',
  imports: [RouterLink, TranslatePipe],
  templateUrl: './profile-menu.html',
})
export class ProfileMenu {
  private readonly auth = inject(AuthService);
  protected readonly theme = inject(ThemeService);
  protected readonly features = inject(FeaturesService);

  protected readonly themeModes = THEME_MODES;
  private readonly runtimeConfig = inject(RuntimeConfigService);
  protected readonly version = this.runtimeConfig.version;
  protected readonly repositoryUrl = this.runtimeConfig.repositoryUrl;

  private readonly injector = inject(Injector);
  private readonly trigger = viewChild.required<ElementRef<HTMLButtonElement>>('trigger');
  private readonly panel = viewChild<ElementRef<HTMLElement>>('panel');

  protected readonly open = signal(false);

  protected toggle(): void {
    this.open.update((value) => !value);

    if (this.open()) {
      afterNextRender(() => this.focusFirstItem(), { injector: this.injector });
    }
  }

  // Runs once the panel has rendered, so keyboard users land inside the menu they just opened.
  private focusFirstItem(): void {
    this.panel()?.nativeElement.querySelector<HTMLElement>('a, button')?.focus();
  }

  protected close(): void {
    this.open.set(false);
  }

  // Backdrop click / Escape: close and hand focus back to the toggle, since the focused control
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

  protected setTheme(mode: ThemeMode): void {
    this.theme.setMode(mode);
  }

  protected logout(): void {
    this.close();
    this.auth.logout();
  }
}

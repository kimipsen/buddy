import {
  Component,
  ElementRef,
  HostListener,
  Injector,
  computed,
  afterNextRender,
  inject,
  signal,
  viewChild,
} from '@angular/core';

import { RouterLink } from '@angular/router';

import { AuthService } from '../../../../core/auth.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { THEME_MODES, ThemeMode } from '../../../../core/theme';
import { ThemeService } from '../../../../core/theme.service';
import { UsersService } from '../../../../core/users.service';

@Component({
  selector: 'app-child-menu',
  imports: [RouterLink, TranslatePipe],
  templateUrl: './child-menu.html',
})
export class ChildMenu {
  private readonly auth = inject(AuthService);
  protected readonly theme = inject(ThemeService);
  private readonly users = inject(UsersService);

  // "My timer" only while a guardian has timers on (the page's guard says the same).
  protected readonly timersOn = computed(() => this.users.currentUser()?.showVisualTimers === true);

  protected readonly themeModes = THEME_MODES;

  private readonly injector = inject(Injector);
  private readonly trigger = viewChild.required<ElementRef<HTMLButtonElement>>('trigger');
  private readonly panel = viewChild<ElementRef<HTMLElement>>('panel');

  protected readonly open = signal(false);

  protected toggle(): void {
    this.open.update((value) => !value);

    // Stryker disable next-line ConditionalExpression: after closing, the panel is gone, so focusFirstItem() finds nothing to focus
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

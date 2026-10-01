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

import { AuthService } from '../../../../core/auth.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { UsersService } from '../../../../core/users.service';

@Component({
  selector: 'app-delete-account',
  imports: [TranslatePipe],
  templateUrl: './delete-account.html',
})
export class DeleteAccount {
  private readonly users = inject(UsersService);
  private readonly auth = inject(AuthService);
  private readonly injector = inject(Injector);

  private readonly trigger = viewChild.required<ElementRef<HTMLButtonElement>>('trigger');
  private readonly panel = viewChild<ElementRef<HTMLElement>>('panel');
  private readonly cancelButton = viewChild<ElementRef<HTMLButtonElement>>('cancelButton');

  protected readonly confirmOpen = signal(false);
  protected readonly deleting = signal(false);
  protected readonly error = signal<string | null>(null);

  protected openConfirm(): void {
    this.error.set(null);
    this.confirmOpen.set(true);
    // Start on Cancel, the safe choice, once the dialog has rendered.
    afterNextRender(() => this.cancelButton()?.nativeElement.focus(), { injector: this.injector });
  }

  protected closeConfirm(): void {
    if (this.deleting()) {
      return;
    }

    this.confirmOpen.set(false);
    this.trigger().nativeElement.focus();
  }

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    if (this.confirmOpen()) {
      this.closeConfirm();
    }
  }

  // aria-modal promises the page behind is inert, so keep Tab / Shift+Tab cycling inside the
  // dialog. While a delete is in flight both buttons are disabled and focus rests on the panel.
  @HostListener('document:keydown.tab', ['$event'])
  @HostListener('document:keydown.shift.tab', ['$event'])
  protected onTab(event: KeyboardEvent): void {
    const panel = this.panel()?.nativeElement;

    if (!panel) {
      return;
    }

    const buttons: HTMLElement[] = Array.from(
      panel.querySelectorAll<HTMLButtonElement>('button:not(:disabled)'),
    );
    const first = buttons.at(0) ?? panel;
    const last = buttons.at(-1) ?? panel;
    const active = document.activeElement as HTMLElement | null;

    // Let the browser move between the enabled buttons; take over at either edge and whenever
    // focus is anywhere else (the panel itself, a just-disabled button, or outside the dialog).
    const insideRun = active !== null && buttons.includes(active);
    if (!insideRun || active === (event.shiftKey ? first : last)) {
      event.preventDefault();
      (event.shiftKey ? last : first).focus();
    }
  }

  protected async confirmDelete(): Promise<void> {
    this.deleting.set(true);
    this.error.set(null);

    try {
      await this.users.deleteCurrentUser();
      this.auth.logout();
    } catch {
      this.error.set('admin.deleteAccount.error');
      this.deleting.set(false);
    }
  }
}

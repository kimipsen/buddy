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
import { AccountDeletionPreview, UsersService } from '../../../../core/users.service';
import { createAction } from '../../../../shared/action-state/action-state';

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
  protected readonly deleting = createAction();

  // What the deletion takes with it (children with no other guardian, owned groups), fetched each
  // time the dialog opens. A failed fetch doesn't block the deletion; the dialog just says so.
  protected readonly preview = signal<AccountDeletionPreview | null>(null);
  protected readonly previewFailed = signal(false);

  protected openConfirm(): void {
    this.deleting.reset();
    this.preview.set(null);
    this.previewFailed.set(false);
    void this.loadPreview();
    this.confirmOpen.set(true);
    // Start on Cancel, the safe choice, once the dialog has rendered.
    afterNextRender(() => this.cancelButton()?.nativeElement.focus(), { injector: this.injector });
  }

  protected closeConfirm(): void {
    if (this.deleting.busy()) {
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
  protected onTab(event: Event): void {
    const panel = this.panel()?.nativeElement;

    if (!panel || !(event instanceof KeyboardEvent)) {
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

  private async loadPreview(): Promise<void> {
    try {
      this.preview.set(await this.users.getAccountDeletionPreview());
    } catch {
      this.previewFailed.set(true);
    }
  }

  protected async confirmDelete(): Promise<void> {
    // logout() navigates away to Keycloak, so the dialog never renders the idle state that
    // follows a successful delete.
    await this.deleting.run(
      true,
      async () => {
        await this.users.deleteCurrentUser();
        this.auth.logout();
      },
      'admin.deleteAccount.error',
    );
  }
}

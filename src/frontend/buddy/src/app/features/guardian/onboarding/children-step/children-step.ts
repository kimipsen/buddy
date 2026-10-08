import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { GroupsService } from '../../../../core/groups.service';
import { ChildSummary, GuardiansService } from '../../../../core/guardians.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { OnboardingSetup } from '../../../../core/onboarding.service';
import { createAction } from '../../../../shared/action-state/action-state';

// A one-time password shown right after creating a child or resetting theirs. Held only in this
// component's memory: never in progress, the URL or browser storage.
interface ShownCredentials {
  givenName: string;
  username: string;
  temporaryPassword: string;
}

// Step 2: create each child's account and add it to the setup group. A child whose membership
// failed stays listed outside the group with a retry, so the account is never created twice.
@Component({
  selector: 'app-onboarding-children-step',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './children-step.html',
})
export class ChildrenStep {
  private readonly guardians = inject(GuardiansService);
  private readonly groups = inject(GroupsService);

  readonly setup = input.required<OnboardingSetup>();
  readonly changed = output<void>();

  protected readonly givenName = signal('');
  protected readonly familyName = signal('');
  protected readonly username = signal('');
  protected readonly adding = createAction();
  protected readonly joining = createAction<string>();
  protected readonly resetting = createAction<string>();
  protected readonly credentials = signal<ShownCredentials | null>(null);
  protected readonly copied = signal(false);

  protected async add(): Promise<void> {
    const givenName = this.givenName().trim();
    const familyName = this.familyName().trim();
    const username = this.username().trim();
    const groupId = this.setup().group?.id;

    if (!givenName || !familyName || !username || groupId === undefined) {
      return;
    }

    const created: { id?: string } = {};
    await this.adding.run(
      true,
      async () => {
        const child = await this.guardians.createChild({ givenName, familyName, username });
        created.id = child.id;
        this.showCredentials(givenName, child.username, child.temporaryPassword);
        this.givenName.set('');
        this.familyName.set('');
        this.username.set('');
      },
      (error) =>
        error instanceof HttpErrorResponse && error.status === 409
          ? 'onboarding.children.usernameTaken'
          : 'onboarding.children.addError',
    );

    if (created.id !== undefined) {
      // A failure here leaves the child outside the group, where the list offers a retry.
      await this.joinGroup(created.id, groupId);
      this.changed.emit();
    }
  }

  protected async addToGroup(child: ChildSummary): Promise<void> {
    const groupId = this.setup().group?.id;

    if (groupId !== undefined && (await this.joinGroup(child.id, groupId))) {
      this.changed.emit();
    }
  }

  protected async resetPassword(child: ChildSummary): Promise<void> {
    await this.resetting.run(
      child.id,
      async () => {
        const reset = await this.guardians.resetChildPassword(child.id);
        this.showCredentials(child.name.givenName, reset.username, reset.temporaryPassword);
      },
      'onboarding.children.resetError',
    );
  }

  protected async copy(password: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(password);
      this.copied.set(true);
    } catch {
      this.copied.set(false);
    }
  }

  private joinGroup(childId: string, groupId: string): Promise<boolean> {
    return this.joining.run(
      childId,
      () => this.groups.addChildToGroup(groupId, childId),
      'onboarding.children.addToGroupError',
    );
  }

  private showCredentials(givenName: string, username: string, temporaryPassword: string): void {
    this.credentials.set({ givenName, username, temporaryPassword });
    this.copied.set(false);
  }
}

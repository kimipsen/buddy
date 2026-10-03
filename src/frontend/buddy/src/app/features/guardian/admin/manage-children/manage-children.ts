import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { listTimeZoneIds } from '../../../../core/date-utils';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import {
  LANGUAGE_NAMES,
  SUPPORTED_LANGUAGES,
  isSupportedLanguage,
} from '../../../../core/i18n/language';
import {
  ChildSummary,
  CreateChildResult,
  GuardianKind,
  GuardiansService,
} from '../../../../core/guardians.service';
import { createAction } from '../../../../shared/action-state/action-state';

const INVITABLE_KINDS: GuardianKind[] = [0, 1];

const KIND_LABELS: Record<GuardianKind, string> = {
  0: 'admin.manageChildren.invite.kinds.parent',
  1: 'admin.manageChildren.invite.kinds.guardian',
};

@Component({
  selector: 'app-manage-children',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './manage-children.html',
})
export class ManageChildren {
  private readonly guardians = inject(GuardiansService);

  protected readonly invitableKinds = INVITABLE_KINDS;
  protected readonly kindLabels = KIND_LABELS;
  protected readonly languages = SUPPORTED_LANGUAGES;
  protected readonly languageNames = LANGUAGE_NAMES;
  protected readonly timeZoneIds = listTimeZoneIds();

  protected readonly children = resource({ loader: () => this.guardians.listMyChildren() });

  protected readonly newChildGivenName = signal('');
  protected readonly newChildFamilyName = signal('');
  protected readonly newChildUsername = signal('');
  protected readonly addingChild = createAction();
  protected readonly lastCreatedChild = signal<CreateChildResult | null>(null);

  protected readonly confirmingRevokeChildId = signal<string | null>(null);
  protected readonly revokingChild = createAction<string>();

  // One save at a time per setting, but each child keeps its own error until its next attempt.
  protected readonly savingLanguageChildId = signal<string | null>(null);
  protected readonly languageErrorByChildId = signal<Record<string, string>>({});

  protected readonly savingTimeZoneChildId = signal<string | null>(null);
  protected readonly timeZoneErrorByChildId = signal<Record<string, string>>({});

  // Stryker disable next-line BooleanLiteral: the copy button only renders once lastCreatedChild is set, and addChild resets this to false at the same time
  protected readonly passwordCopied = signal(false);

  protected readonly expandedInviteChildId = signal<string | null>(null);
  // The expanded child's pending invites; idle while no invite panel is open.
  protected readonly invites = resource({
    params: () => this.expandedInviteChildId() ?? undefined,
    loader: ({ params: childId }) => this.guardians.listGuardianInvites(childId),
  });

  // Stryker disable next-line StringLiteral: the invite form only renders once toggleInvitePanel has reset this to ''
  protected readonly inviteEmail = signal('');
  protected readonly inviteKind = signal<GuardianKind>(0);
  protected readonly sendingInvite = createAction();
  protected readonly revokingInvite = createAction<string>();

  protected async addChild(): Promise<void> {
    const givenName = this.newChildGivenName().trim();
    const familyName = this.newChildFamilyName().trim();
    const username = this.newChildUsername().trim();

    if (!givenName || !familyName || !username) {
      return;
    }

    await this.addingChild.run(
      true,
      async () => {
        const created = await this.guardians.createChild({ givenName, familyName, username });
        this.lastCreatedChild.set(created);
        this.passwordCopied.set(false);
        this.newChildGivenName.set('');
        this.newChildFamilyName.set('');
        this.newChildUsername.set('');
        this.children.reload();
      },
      (error) =>
        error instanceof HttpErrorResponse && error.status === 409
          ? 'admin.manageChildren.usernameTakenError'
          : 'admin.manageChildren.addError',
    );
  }

  protected requestRevoke(childId: string): void {
    this.revokingChild.clearError();
    this.confirmingRevokeChildId.set(childId);
  }

  protected cancelRevoke(): void {
    this.confirmingRevokeChildId.set(null);
  }

  protected async confirmRevoke(childId: string): Promise<void> {
    await this.revokingChild.run(
      childId,
      async () => {
        await this.guardians.revokeChild(childId);
        this.confirmingRevokeChildId.set(null);
        this.children.reload();
      },
      'admin.manageChildren.revokeError',
    );
  }

  protected languageErrorFor(childId: string): string | undefined {
    return this.languageErrorByChildId()[childId];
  }

  protected async changeLanguage(childId: string, language: string): Promise<void> {
    if (!isSupportedLanguage(language)) {
      return;
    }

    this.savingLanguageChildId.set(childId);
    this.languageErrorByChildId.update((byChildId) => withoutKey(byChildId, childId));

    try {
      const updated = await this.guardians.updateChildLanguage(childId, language);
      this.replaceChild(childId, updated);
    } catch {
      this.languageErrorByChildId.update((byChildId) => ({
        ...byChildId,
        [childId]: 'admin.manageChildren.language.error',
      }));
    } finally {
      this.savingLanguageChildId.set(null);
    }
  }

  protected timeZoneErrorFor(childId: string): string | undefined {
    return this.timeZoneErrorByChildId()[childId];
  }

  protected async changeTimeZone(childId: string, timeZoneId: string): Promise<void> {
    if (!timeZoneId) {
      return;
    }

    this.savingTimeZoneChildId.set(childId);
    this.timeZoneErrorByChildId.update((byChildId) => withoutKey(byChildId, childId));

    try {
      const updated = await this.guardians.updateChildTimeZone(childId, timeZoneId);
      this.replaceChild(childId, updated);
    } catch {
      this.timeZoneErrorByChildId.update((byChildId) => ({
        ...byChildId,
        [childId]: 'admin.manageChildren.timeZone.error',
      }));
    } finally {
      this.savingTimeZoneChildId.set(null);
    }
  }

  protected toggleInvitePanel(childId: string): void {
    if (this.expandedInviteChildId() === childId) {
      this.expandedInviteChildId.set(null);
      return;
    }

    this.expandedInviteChildId.set(childId);
    this.inviteEmail.set('');
    this.inviteKind.set(0);
    this.sendingInvite.clearError();
    this.revokingInvite.clearError();
  }

  protected async sendGuardianInvite(childId: string): Promise<void> {
    const email = this.inviteEmail().trim();

    if (!email) {
      return;
    }

    await this.sendingInvite.run(
      true,
      async () => {
        await this.guardians.inviteGuardian(childId, { email, kind: this.inviteKind() });
        this.inviteEmail.set('');
        this.revokingInvite.clearError();
        this.invites.reload();
      },
      'admin.manageChildren.invite.sendError',
    );
  }

  protected async revokeGuardianInvite(childId: string, inviteId: string): Promise<void> {
    await this.revokingInvite.run(
      inviteId,
      async () => {
        await this.guardians.revokeGuardianInvite(childId, inviteId);
        this.invites.reload();
      },
      'admin.manageChildren.invite.cancelError',
    );
  }

  protected async copyPassword(password: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(password);
      this.passwordCopied.set(true);
    } catch {
      this.passwordCopied.set(false);
    }
  }

  private replaceChild(childId: string, updated: ChildSummary): void {
    // While a reload is in flight (or after it failed), updating would cancel it or throw; the
    // reload brings the fresh child anyway.
    if (!this.children.hasValue() || this.children.isLoading()) {
      return;
    }

    this.children.update((list) => list.map((child) => (child.id === childId ? updated : child)));
  }
}

function withoutKey(byChildId: Record<string, string>, childId: string): Record<string, string> {
  return Object.fromEntries(Object.entries(byChildId).filter(([id]) => id !== childId));
}

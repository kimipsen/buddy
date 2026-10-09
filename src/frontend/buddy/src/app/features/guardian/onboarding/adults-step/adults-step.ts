import { Component, computed, inject, input, linkedSignal, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { GroupRole, GroupsService } from '../../../../core/groups.service';
import { GuardianKind, GuardiansService } from '../../../../core/guardians.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { OnboardingSetup } from '../../../../core/onboarding.service';
import {
  SegmentedControl,
  SegmentedControlOption,
} from '../../../../shared/segmented-control/segmented-control';
import { Toggle } from '../../../../shared/toggle/toggle';

// The group roles an adult can be invited with: never another Owner.
type InvitedRole = Exclude<GroupRole, 'Owner'>;

type RequestStatus = 'pending' | 'sent' | 'failed';

// One invitation the guardian asked for: a guardian invite per selected child, plus the group
// invite. Each is tracked on its own, so a retry resends only the ones that failed.
interface InviteRequest {
  key: string;
  childId: string | null;
  label: string;
  status: RequestStatus;
}

interface InviteBatch {
  email: string;
  kind: GuardianKind;
  role: InvitedRole;
}

const STATUS_LABELS: Record<RequestStatus, string> = {
  pending: 'onboarding.adults.status.pending',
  sent: 'onboarding.adults.status.sent',
  failed: 'onboarding.adults.status.failed',
};

// Step 3 (optional): invite other adults. Group membership and guardianship are separate
// invitations (inviteToGroup never creates a GuardianLink, inviteGuardian never adds to the group),
// and the group role has no default: the guardian picks it knowing what each one allows.
@Component({
  selector: 'app-onboarding-adults-step',
  imports: [FormsModule, TranslatePipe, SegmentedControl, Toggle],
  templateUrl: './adults-step.html',
})
export class AdultsStep {
  private readonly groups = inject(GroupsService);
  private readonly guardians = inject(GuardiansService);
  private readonly i18n = inject(TranslationService);

  readonly setup = input.required<OnboardingSetup>();
  readonly skipped = input(false);
  readonly busy = input(false);
  readonly changed = output<void>();
  readonly skip = output<void>();

  protected readonly statusLabels = STATUS_LABELS;

  protected readonly email = signal('');
  protected readonly kind = signal<GuardianKind>('Parent');
  protected readonly role = signal<InvitedRole | null>(null);
  protected readonly roleMissing = signal(false);
  // Every setup child starts selected; the guardian can untick any, and that survives a reload
  // (a child new to the setup since is selected).
  protected readonly selectedChildIds = linkedSignal<string[], string[]>({
    source: () => this.setup().children.map((child) => child.id),
    computation: (ids, previous) =>
      previous === undefined
        ? ids
        : ids.filter((id) => previous.value.includes(id) || !previous.source.includes(id)),
  });

  private readonly batch = signal<InviteBatch | null>(null);
  protected readonly requests = signal<InviteRequest[]>([]);
  protected readonly sending = computed(() =>
    this.requests().some((request) => request.status === 'pending'),
  );
  protected readonly hasFailures = computed(() =>
    this.requests().some((request) => request.status === 'failed'),
  );

  protected readonly kindOptions = computed<SegmentedControlOption<GuardianKind>[]>(() => [
    { value: 'Parent', label: this.i18n.translate('onboarding.adults.kinds.parent') },
    { value: 'Guardian', label: this.i18n.translate('onboarding.adults.kinds.guardian') },
  ]);

  protected readonly roleOptions = computed<SegmentedControlOption<InvitedRole | null>[]>(() => [
    { value: 'Admin', label: this.i18n.translate('onboarding.adults.roles.admin') },
    { value: 'Member', label: this.i18n.translate('onboarding.adults.roles.member') },
  ]);

  protected isSelected(childId: string): boolean {
    return this.selectedChildIds().includes(childId);
  }

  protected setSelected(childId: string, selected: boolean): void {
    this.selectedChildIds.update((ids) =>
      selected ? [...ids, childId] : ids.filter((id) => id !== childId),
    );
  }

  protected chooseRole(role: InvitedRole | null): void {
    this.role.set(role);
    this.roleMissing.set(false);
  }

  protected async send(): Promise<void> {
    const email = this.email().trim();
    const role = this.role();

    if (role === null) {
      this.roleMissing.set(true);
      return;
    }

    if (!email || this.sending()) {
      return;
    }

    const children = this.setup().children.filter((child) => this.isSelected(child.id));
    this.batch.set({ email, kind: this.kind(), role });
    this.requests.set([
      ...children.map((child) => ({
        key: `guardian:${child.id}`,
        childId: child.id,
        label: this.i18n.translate('onboarding.adults.requestGuardian', {
          name: child.name.givenName,
        }),
        status: 'pending' as const,
      })),
      {
        key: 'group',
        childId: null,
        label: this.i18n.translate('onboarding.adults.requestGroup'),
        status: 'pending',
      },
    ]);

    await this.runPending();
  }

  protected async retry(): Promise<void> {
    this.requests.update((requests) =>
      requests.map((request) =>
        request.status === 'failed' ? { ...request, status: 'pending' } : request,
      ),
    );
    await this.runPending();
  }

  // Sends every pending request one after another; a failure doesn't stop the rest.
  private async runPending(): Promise<void> {
    const batch = this.batch();
    const groupId = this.setup().group?.id;

    if (batch === null || groupId === undefined) {
      return;
    }

    for (const request of this.requests().filter((r) => r.status === 'pending')) {
      let status: RequestStatus = 'sent';

      try {
        if (request.childId === null) {
          await this.groups.inviteToGroup(groupId, { email: batch.email, role: batch.role });
        } else {
          await this.guardians.inviteGuardian(request.childId, {
            email: batch.email,
            kind: batch.kind,
          });
        }
      } catch {
        status = 'failed';
      }

      this.requests.update((requests) =>
        requests.map((r) => (r.key === request.key ? { ...r, status } : r)),
      );
    }

    if (!this.hasFailures()) {
      // Ready for the next adult; the sent list stays visible until then.
      this.email.set('');
      this.role.set(null);
      this.batch.set(null);
    }

    this.changed.emit();
  }
}

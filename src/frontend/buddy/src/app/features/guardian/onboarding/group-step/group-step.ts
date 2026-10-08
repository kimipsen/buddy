import { Component, computed, inject, input, output, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { GroupsService } from '../../../../core/groups.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { OnboardingSetup } from '../../../../core/onboarding.service';
import { createAction } from '../../../../shared/action-state/action-state';

// Step 1: one group for the rest of the setup. A guardian whose setup group is gone, or who created
// one before an interrupted reload, picks a group they manage instead of getting a duplicate.
@Component({
  selector: 'app-onboarding-group-step',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './group-step.html',
})
export class GroupStep {
  private readonly groups = inject(GroupsService);

  readonly setup = input.required<OnboardingSetup>();
  // True while the page saves the chosen group into progress.
  readonly busy = input(false);
  readonly groupChosen = output<string>();

  protected readonly name = signal('');
  protected readonly creating = createAction();
  // The last group created here, kept so a failed progress save retries with it, not a new group.
  protected readonly createdGroupId = signal<string | null>(null);

  // Owner (0) or Admin (1): the backend only accepts a setup group the guardian manages.
  private readonly myGroups = resource({ loader: () => this.groups.listMyGroups() });
  protected readonly manageableGroups = computed(() =>
    (this.myGroups.hasValue() ? this.myGroups.value() : []).filter(
      (group) => group.role !== 2 && group.id !== this.setup().group?.id,
    ),
  );
  protected readonly groupsError = computed(() => this.myGroups.error() !== undefined);

  protected async create(): Promise<void> {
    const name = this.name().trim();

    if (!name) {
      return;
    }

    const pending = this.createdGroupId();

    if (pending !== null) {
      this.groupChosen.emit(pending);
      return;
    }

    await this.creating.run(
      true,
      async () => {
        const group = await this.groups.createGroup({ name });
        this.createdGroupId.set(group.id);
        this.groupChosen.emit(group.id);
      },
      'onboarding.group.createError',
    );
  }

  protected choose(groupId: string): void {
    this.groupChosen.emit(groupId);
  }
}

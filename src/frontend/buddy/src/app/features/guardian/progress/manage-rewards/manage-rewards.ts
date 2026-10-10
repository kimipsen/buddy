import { Component, computed, inject, linkedSignal, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';

import { GuardiansService } from '../../../../core/guardians.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import {
  INSUFFICIENT_STARS,
  ProgressService,
  ProgressSummary,
  REWARD_REQUEST_RESOLVED,
  Reward,
  RewardDraft,
  RewardRequest,
  RewardRequestStatus,
  errorCode,
} from '../../../../core/progress.service';
import { createAction } from '../../../../shared/action-state/action-state';
import { Card } from '../../../../shared/card/card';

interface RewardRow {
  // Undefined for a row added in this draft; the server assigns its id on save.
  id: string | undefined;
  name: string;
  icon: string;
  cost: string;
}

function toRow(reward: Reward): RewardRow {
  return { id: reward.id, name: reward.name, icon: reward.icon, cost: String(reward.cost) };
}

// ConfigureRewardsValidator.MaxCost.
const MAX_COST = 10_000;

const STATUS_KEYS: Record<Exclude<RewardRequestStatus, 'Pending'>, string> = {
  Approved: 'progress.manageRewards.status.Approved',
  Declined: 'progress.manageRewards.status.Declined',
  Cancelled: 'progress.manageRewards.status.Cancelled',
};

// A child's reward catalog and the requests they've made from it: approve or decline what's
// waiting, see recent outcomes, and edit the catalog (a full replace, like the goal posts).
// See docs/backend/analysis/reward-redemption.md.
@Component({
  selector: 'app-manage-rewards',
  imports: [FormsModule, TranslatePipe, Card],
  templateUrl: './manage-rewards.html',
})
export class ManageRewards {
  private readonly guardians = inject(GuardiansService);
  private readonly progressService = inject(ProgressService);
  // ?child=<id> preselects a child (the dashboard's "Rewards waiting" link). Optional so the card
  // also works outside a routed page.
  private readonly requestedChildId =
    inject(ActivatedRoute, { optional: true })?.snapshot.queryParamMap.get('child') ?? undefined;

  protected readonly children = resource({ loader: () => this.guardians.listMyChildren() });
  protected readonly childList = computed(() =>
    this.children.hasValue() ? this.children.value() : [],
  );
  protected readonly selectedChildId = linkedSignal(() => {
    const children = this.childList();
    return (children.find((child) => child.id === this.requestedChildId) ?? children[0])?.id;
  });

  // The selected child's progress; idle until a child is selected.
  protected readonly progress = resource({
    params: () => {
      const childId = this.selectedChildId();
      return childId ? { childId } : undefined;
    },
    loader: ({ params }) => this.progressService.getChildProgress(params.childId),
  });

  private readonly summary = computed(() =>
    this.progress.hasValue() ? this.progress.value() : undefined,
  );
  protected readonly pending = computed(() =>
    (this.summary()?.rewardRequests ?? []).filter((request) => request.status === 'Pending'),
  );
  protected readonly history = computed(() =>
    (this.summary()?.rewardRequests ?? []).filter((request) => request.status !== 'Pending'),
  );

  // Compared by content, so approving or declining a request (a new summary with the same
  // catalog) doesn't throw away unsaved catalog edits. Keyed by child too, so switching between
  // two children with the same (say, empty) catalog still drops the first child's draft.
  private readonly catalog = computed(
    () => ({ childId: this.selectedChildId(), rewards: this.summary()?.rewards ?? [] }),
    { equal: (a, b) => JSON.stringify(a) === JSON.stringify(b) },
  );

  // The editable catalog draft, reset from the loaded (or just saved) rewards.
  protected readonly rows = linkedSignal(() => this.catalog().rewards.map(toRow));

  protected readonly loading = computed(
    () => this.children.isLoading() || this.progress.isLoading(),
  );
  protected readonly hasChildren = computed(
    () => !this.children.hasValue() || this.children.value().length > 0,
  );
  protected readonly loadFailed = computed(
    () => !!this.children.error() || !!this.progress.error(),
  );

  protected readonly saving = createAction();
  protected readonly saved = signal(false);
  // Keyed by request id.
  protected readonly resolving = createAction<string>();

  protected onChildChange(childId: string): void {
    this.selectedChildId.set(childId);
    this.saved.set(false);
    this.saving.clearError();
    this.resolving.reset();
  }

  protected statusKey(request: RewardRequest): string {
    return request.status === 'Pending' ? '' : STATUS_KEYS[request.status];
  }

  protected addRow(): void {
    this.rows.update((rows) => [...rows, { id: undefined, name: '', icon: '🎁', cost: '' }]);
    this.saved.set(false);
  }

  protected removeRow(index: number): void {
    this.rows.update((rows) => rows.filter((_, i) => i !== index));
    this.saved.set(false);
  }

  protected setField(index: number, field: 'name' | 'icon' | 'cost', value: string): void {
    this.rows.update((rows) =>
      rows.map((row, i) => (i === index ? { ...row, [field]: value } : row)),
    );
    this.saved.set(false);
  }

  // An empty list is allowed: it removes every reward. Never before the catalog has loaded,
  // though: an empty draft from a failed load would wipe a catalog the guardian never saw.
  protected canSave(): boolean {
    return (
      this.progress.hasValue() &&
      this.rows().every((row) => {
        const cost = Number(row.cost);
        return (
          row.name.trim().length > 0 &&
          row.icon.trim().length > 0 &&
          Number.isInteger(cost) &&
          cost >= 1 &&
          cost <= MAX_COST
        );
      })
    );
  }

  protected async save(): Promise<void> {
    const childId = this.selectedChildId();

    if (!childId || !this.canSave()) {
      return;
    }

    this.saved.set(false);

    await this.saving.run(
      true,
      async () => {
        const rewards: RewardDraft[] = this.rows().map((row) => ({
          ...(row.id ? { id: row.id } : {}),
          name: row.name.trim(),
          icon: row.icon.trim(),
          cost: Number(row.cost),
        }));

        let summary: ProgressSummary;
        try {
          summary = await this.progressService.configureRewards(childId, rewards);
        } catch (error: unknown) {
          // Switched child while saving: the failed draft is no longer on screen, so its error
          // would only be misread as being about the new child (same as ManageProgressGoals).
          if (this.selectedChildId() !== childId) {
            return;
          }
          throw error;
        }

        if (this.apply(childId, summary)) {
          this.saved.set(true);
        }
      },
      'progress.manageRewards.saveError',
    );
  }

  protected approve(request: RewardRequest): Promise<void> {
    return this.resolve(request, (childId) =>
      this.progressService.approveRewardRequest(childId, request.id),
    );
  }

  protected decline(request: RewardRequest): Promise<void> {
    return this.resolve(request, (childId) =>
      this.progressService.declineRewardRequest(childId, request.id),
    );
  }

  private async resolve(
    request: RewardRequest,
    send: (childId: string) => Promise<ProgressSummary>,
  ): Promise<void> {
    const childId = this.selectedChildId();

    if (!childId) {
      return;
    }

    await this.resolving.run(
      request.id,
      async () => {
        try {
          this.apply(childId, await send(childId));
        } catch (error: unknown) {
          // Handled elsewhere meanwhile (the child withdrew it, another guardian answered):
          // reload so the list shows what actually happened.
          if (errorCode(error) === REWARD_REQUEST_RESOLVED) {
            this.progress.reload();
          }
          throw error;
        }
      },
      (error) => {
        switch (errorCode(error)) {
          case INSUFFICIENT_STARS:
            return 'progress.manageRewards.insufficientStars';
          case REWARD_REQUEST_RESOLVED:
            return 'progress.manageRewards.alreadyResolved';
          default:
            return 'progress.manageRewards.resolveError';
        }
      },
    );
  }

  // Shows a response, unless the guardian switched child meanwhile (it's the previous child's
  // progress, and setting it would cancel or overwrite the new child's load).
  private apply(childId: string, summary: ProgressSummary): boolean {
    if (this.selectedChildId() !== childId || this.progress.isLoading()) {
      return false;
    }

    this.progress.set(summary);
    return true;
  }
}

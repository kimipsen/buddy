import { Component, computed, inject, resource } from '@angular/core';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import {
  INSUFFICIENT_STARS,
  ProgressService,
  ProgressSummary,
  Reward,
  RewardRequest,
  RewardRequestStatus,
  TOO_MANY_PENDING_REQUESTS,
  errorCode,
} from '../../../core/progress.service';
import { createAction } from '../../../shared/action-state/action-state';
import { LoadingSpinner } from '../../../shared/loading-spinner/loading-spinner';
import { ChildPage } from '../child-page/child-page';

const STATUS_KEYS: Record<Exclude<RewardRequestStatus, 'Pending'>, string> = {
  Approved: 'child.rewards.status.Approved',
  Declined: 'child.rewards.status.Declined',
  Cancelled: 'child.rewards.status.Cancelled',
};

// The child spends stars here: their balance, the rewards a guardian set up, the requests still
// waiting for a grown-up, and how earlier ones went (docs/backend/analysis/reward-redemption.md).
@Component({
  selector: 'app-child-rewards',
  imports: [ChildPage, LoadingSpinner, TranslatePipe],
  templateUrl: './child-rewards.html',
})
export class ChildRewards {
  private readonly progressService = inject(ProgressService);

  protected readonly progress = resource({ loader: () => this.progressService.getMyProgress() });

  protected readonly spendable = computed(() =>
    this.progress.hasValue() ? this.progress.value().spendableStars : 0,
  );
  protected readonly rewards = computed(() =>
    this.progress.hasValue() ? this.progress.value().rewards : [],
  );
  protected readonly pending = computed(() =>
    this.requests().filter((request) => request.status === 'Pending'),
  );
  protected readonly history = computed(() =>
    this.requests().filter((request) => request.status !== 'Pending'),
  );

  // Keyed by reward id.
  protected readonly requesting = createAction<string>();
  // Keyed by request id. Its error shows under the balance, not in the request's row, because a
  // failed cancel reloads and the request has usually left the waiting list by then.
  protected readonly cancelling = createAction<string>();
  protected readonly busy = computed(() => this.requesting.busy() || this.cancelling.busy());

  protected canAfford(reward: Reward): boolean {
    return reward.cost <= this.spendable();
  }

  protected statusKey(request: RewardRequest): string {
    return request.status === 'Pending' ? '' : STATUS_KEYS[request.status];
  }

  protected async request(reward: Reward): Promise<void> {
    this.cancelling.reset();
    await this.requesting.run(
      reward.id,
      async () => {
        try {
          this.update(await this.progressService.requestReward(reward.id));
        } catch (error: unknown) {
          // The balance moved (a star was taken back, or another device asked first): show the
          // real one alongside the message.
          if (errorCode(error) === INSUFFICIENT_STARS) {
            this.progress.reload();
          }
          throw error;
        }
      },
      (error) => {
        switch (errorCode(error)) {
          case INSUFFICIENT_STARS:
            return 'child.rewards.insufficientStars';
          case TOO_MANY_PENDING_REQUESTS:
            return 'child.rewards.tooManyPending';
          default:
            return 'child.rewards.requestError';
        }
      },
    );
  }

  protected async cancel(request: RewardRequest): Promise<void> {
    this.requesting.reset();
    await this.cancelling.run(
      request.id,
      async () => {
        try {
          this.update(await this.progressService.cancelRewardRequest(request.id));
        } catch (error: unknown) {
          // Already approved or declined meanwhile: reload so the child sees how it went.
          this.progress.reload();
          throw error;
        }
      },
      'child.rewards.cancelError',
    );
  }

  private requests(): RewardRequest[] {
    return this.progress.hasValue() ? this.progress.value().rewardRequests : [];
  }

  // Only once loaded: setting the resource mid-load would cancel the load.
  private update(summary: ProgressSummary): void {
    if (!this.progress.isLoading()) {
      this.progress.set(summary);
    }
  }
}

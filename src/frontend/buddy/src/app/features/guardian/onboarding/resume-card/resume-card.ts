import { Component, computed, inject, resource } from '@angular/core';
import { RouterLink } from '@angular/router';

import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { ONBOARDING_STATUS, OnboardingService } from '../../../../core/onboarding.service';

// The dashboard's way back into a guide the guardian chose to finish later. Shows nothing for any
// other state, or when the progress can't be read.
@Component({
  selector: 'app-onboarding-resume-card',
  imports: [RouterLink, TranslatePipe],
  templateUrl: './resume-card.html',
})
export class OnboardingResumeCard {
  private readonly onboarding = inject(OnboardingService);

  private readonly progress = resource({ loader: () => this.onboarding.getProgress() });

  // hasValue() first: value() throws while the resource is in its error state.
  protected readonly deferred = computed(
    () => this.progress.hasValue() && this.progress.value().status === ONBOARDING_STATUS.deferred,
  );
}

import { Component, input } from '@angular/core';

import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { OnboardingSetup } from '../../../../core/onboarding.service';

// Step 7: what the setup created, read from current data. Finishing is the page's job.
@Component({
  selector: 'app-onboarding-summary-step',
  imports: [TranslatePipe],
  templateUrl: './summary-step.html',
})
export class SummaryStep {
  readonly setup = input.required<OnboardingSetup>();
  readonly invitationsSkipped = input(false);
}

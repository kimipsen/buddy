import { Component, inject, input, output } from '@angular/core';

import { AiAssistantService, AiProviderSettings } from '../../../../core/ai-assistant.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { createAction } from '../../../../shared/action-state/action-state';
import { Card } from '../../../../shared/card/card';

// What the AI assistant sends to the family's provider (GDPR Question 6.3 in
// docs/backend/analysis/gdpr-data-protection.md). Shown before the family's first session and on the
// provider settings page. A guardian acknowledges it once for the whole family.
@Component({
  selector: 'app-ai-data-sharing-notice',
  imports: [TranslatePipe, Card],
  templateUrl: './ai-data-sharing-notice.html',
})
export class AiDataSharingNotice {
  private readonly aiAssistant = inject(AiAssistantService);

  readonly childId = input.required<string>();
  readonly acknowledgedAt = input<string | null>(null);
  // False hides the button (e.g. no provider configured yet, so there's nothing to acknowledge).
  readonly canAcknowledge = input(true);
  readonly acknowledged = output<AiProviderSettings>();

  protected readonly acknowledging = createAction();

  protected async acknowledge(): Promise<void> {
    await this.acknowledging.run(
      true,
      async () => {
        this.acknowledged.emit(await this.aiAssistant.acknowledgeDataSharing(this.childId()));
      },
      'mealplan.aiAssistant.dataSharing.acknowledgeError',
    );
  }
}

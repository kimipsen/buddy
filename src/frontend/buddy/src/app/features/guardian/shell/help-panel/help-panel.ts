import { Component, computed, inject, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';

import { FeaturesService } from '../../../../core/features.service';
import { HelpTopic, topicKey } from '../../../../core/help/help-topic';
import { findHelpTopic } from '../../../../core/help/help-topics';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { HelpContent } from '../../../../shared/help-content/help-content';

// The current page's help, expanded inline under the shell header (no overlay -- the app has no
// modals, see docs/frontend/analysis/in-app-help.md, Decision 1).
@Component({
  selector: 'app-help-panel',
  imports: [RouterLink, TranslatePipe, HelpContent],
  templateUrl: './help-panel.html',
})
export class HelpPanel {
  readonly topic = input.required<HelpTopic>();
  readonly panelId = input.required<string>();
  readonly closed = output();

  private readonly features = inject(FeaturesService);

  protected readonly titleKey = computed(() => topicKey(this.topic(), 'title'));

  protected readonly related = computed(() =>
    (this.topic().related ?? [])
      .filter((id) => {
        const related = findHelpTopic(id);
        return related === undefined || this.features.offers(related);
      })
      .map((id) => ({ id, titleKey: `help.topics.${id}.title` })),
  );
}

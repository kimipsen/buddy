import { Component, computed, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';

import { HelpTopic, topicKey } from '../../../../core/help/help-topic';
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

  protected readonly titleKey = computed(() => topicKey(this.topic(), 'title'));

  protected readonly related = computed(() =>
    (this.topic().related ?? []).map((id) => ({ id, titleKey: `help.topics.${id}.title` })),
  );
}

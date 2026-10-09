import { Component, ElementRef, Injector, OnInit, afterNextRender, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { topicKey } from '../../../core/help/help-topic';
import { HELP_TOPICS } from '../../../core/help/help-topics';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { RuntimeConfigService } from '../../../core/runtime-config.service';
import { HelpContent } from '../../../shared/help-content/help-content';

// /guardian/help: every help topic on one page. `?topic=<id>` (the help panel's "All help topics"
// link) scrolls to that topic once it has rendered.
@Component({
  selector: 'app-guardian-help',
  imports: [RouterLink, TranslatePipe, HelpContent],
  templateUrl: './help-page.html',
})
export class GuardianHelp implements OnInit {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);
  private readonly requestedTopic = inject(ActivatedRoute).snapshot.queryParamMap.get('topic');

  protected readonly repositoryUrl = inject(RuntimeConfigService).repositoryUrl;

  protected readonly topics = HELP_TOPICS.map((topic) => ({
    topic,
    anchorId: `help-topic-${topic.id}`,
    titleKey: topicKey(topic, 'title'),
  }));

  ngOnInit(): void {
    const requested = this.requestedTopic;
    if (requested) {
      afterNextRender(() => this.showTopic(requested), { injector: this.injector });
    }
  }

  // Scrolls to the topic and moves focus to its heading, so a keyboard or screen-reader user lands
  // where a sighted user is looking. Router fragments don't scroll (no withInMemoryScrolling).
  // `id` comes from the URL, so only a known topic's anchor is ever looked up.
  protected showTopic(id: string): void {
    const entry = this.topics.find((candidate) => candidate.topic.id === id);
    if (!entry) {
      return;
    }

    const heading = this.host.nativeElement.querySelector<HTMLElement>(`#${entry.anchorId}`);
    heading?.scrollIntoView({ block: 'start' });
    heading?.focus();
  }
}

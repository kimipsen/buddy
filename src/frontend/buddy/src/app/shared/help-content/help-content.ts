import { Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';

import { HelpTopic, sectionKey, stepKeys, topicKey } from '../../core/help/help-topic';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

// One help topic's sections, in registry order. The topic title is left to the host, and
// `headingLevel` fits the section headings under it: the shell's panel titles the topic as an h2,
// the help page as an h3.
@Component({
  selector: 'app-help-content',
  imports: [RouterLink, TranslatePipe],
  templateUrl: './help-content.html',
})
export class HelpContent {
  readonly topic = input.required<HelpTopic>();
  readonly headingLevel = input(3);

  protected readonly sections = computed(() => {
    const topic = this.topic();

    return topic.sections.map((section) => ({
      id: section.id,
      titleKey: sectionKey(topic, section, 'title'),
      bodyKey: sectionKey(topic, section, 'body'),
      stepKeys: stepKeys(topic, section),
    }));
  });

  protected readonly link = computed(() => {
    const topic = this.topic();

    return topic.link ? { route: topic.link, labelKey: topicKey(topic, 'link') } : null;
  });
}

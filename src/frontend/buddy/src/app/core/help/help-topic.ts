import type { FeatureName } from '../features.service';

// The shape of the in-app help (docs/frontend/analysis/in-app-help.md). The text lives in the
// typed `help` translation area; this registry holds what a dictionary can't: the order of a
// topic's sections and how many numbered steps each one has.
export interface HelpSection {
  // help.topics.<topic>.sections.<id>.title / .body
  readonly id: string;
  // Left out while this installation has the feature turned off.
  readonly feature?: FeatureName;
  // help.topics.<topic>.sections.<id>.steps.s1 .. sN, rendered as an ordered list.
  readonly steps?: number;
}

export interface HelpTopic {
  // Key under help.topics, and the value of a route's `data.helpTopic`.
  readonly id: string;
  // Left out (page, panel and related links) while this installation has the feature turned off.
  readonly feature?: FeatureName;
  readonly sections: readonly HelpSection[];
  // Other topic ids the panel links to.
  readonly related?: readonly string[];
  // An in-app page the topic points to; its label is help.topics.<topic>.link.
  readonly link?: string;
}

export function topicKey(topic: HelpTopic, path: string): string {
  return `help.topics.${topic.id}.${path}`;
}

export function sectionKey(topic: HelpTopic, section: HelpSection, path: string): string {
  return topicKey(topic, `sections.${section.id}.${path}`);
}

export function stepKeys(topic: HelpTopic, section: HelpSection): string[] {
  return Array.from({ length: section.steps ?? 0 }, (_, index) =>
    sectionKey(topic, section, `steps.s${index + 1}`),
  );
}

// Every key the registry implies for a topic -- what the coverage spec checks against `en`.
export function helpTopicKeys(topic: HelpTopic): string[] {
  return [
    topicKey(topic, 'title'),
    ...(topic.link ? [topicKey(topic, 'link')] : []),
    ...topic.sections.flatMap((section) => [
      sectionKey(topic, section, 'title'),
      sectionKey(topic, section, 'body'),
      ...stepKeys(topic, section),
    ]),
  ];
}

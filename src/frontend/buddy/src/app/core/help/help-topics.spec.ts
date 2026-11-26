import { describe, expect, it } from 'vitest';
import { findHelpTopic, HELP_TOPICS } from './help-topics';

describe('findHelpTopic', () => {
  it('has more than one topic, so the "first topic" checks are not vacuous', () => {
    expect(HELP_TOPICS.length).toBeGreaterThan(1);
  });

  it.each(HELP_TOPICS.map((topic) => [topic.id, topic] as const))(
    'resolves %s to itself',
    (id, topic) => {
      expect(findHelpTopic(id)).toBe(topic);
    },
  );

  it('resolves a non-first topic to itself and not to the first topic', () => {
    // Duplicates the identity case on purpose, so the intent stays readable if that case changes.
    const second = HELP_TOPICS[1];
    expect(findHelpTopic(second.id)).toBe(second);
    expect(findHelpTopic(second.id)).not.toBe(HELP_TOPICS[0]);
  });

  it.each(['no-such-topic', '', undefined, null, 42, {}])(
    'returns undefined for the unknown id %j',
    (id) => {
      expect(findHelpTopic(id)).toBeUndefined();
    },
  );
});

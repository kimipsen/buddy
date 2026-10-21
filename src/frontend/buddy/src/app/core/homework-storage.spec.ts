import { createSession } from './homework-session';
import {
  clearHomeworkSession,
  readHomeworkSession,
  writeHomeworkSession,
} from './homework-storage';

const KEY = 'buddy_homework_session';
const session = createSession(
  [{ id: 'maths', title: 'Maths', estimateMinutes: 20 }],
  { focusMinutes: 10, breakMinutes: 2 },
  '2026-10-14',
);

describe('homework storage', () => {
  afterEach(() => {
    localStorage.clear();
    vi.restoreAllMocks();
  });

  it('reads back the session it wrote for the same day', () => {
    writeHomeworkSession({ ...session, startedAtMs: 123 });

    expect(JSON.parse(localStorage.getItem(KEY) ?? 'null')).toEqual({
      ...session,
      startedAtMs: 123,
    });
    expect(readHomeworkSession('2026-10-14')).toEqual({ ...session, startedAtMs: 123 });
  });

  it('reads back a finished session', () => {
    const finished = { ...session, currentIndex: session.steps.length };
    writeHomeworkSession(finished);

    expect(readHomeworkSession('2026-10-14')).toEqual(finished);
  });

  it('throws away a session from another day', () => {
    writeHomeworkSession(session);

    expect(readHomeworkSession('2026-10-15')).toBeNull();
  });

  it('treats junk as no session', () => {
    for (const junk of [
      'not json',
      '42',
      JSON.stringify({ date: '2026-10-14' }),
      JSON.stringify({ ...session, steps: [{ kind: 'nap', minutes: 3 }] }),
      JSON.stringify({ ...session, currentIndex: 99 }),
      JSON.stringify({ ...session, currentIndex: -1 }),
      JSON.stringify({ ...session, startedAtMs: 'soon' }),
      JSON.stringify({ ...session, pausedAtMs: 'soon' }),
      JSON.stringify({ ...session, chunks: { focusMinutes: 10 } }),
      JSON.stringify({ ...session, parts: 'maths' }),
      JSON.stringify({ ...session, parts: [null] }),
      JSON.stringify({ ...session, parts: [{ id: 'maths', estimateMinutes: 20 }] }),
      JSON.stringify({
        ...session,
        parts: [{ id: 'maths', title: 'Maths', estimateMinutes: '20' }],
      }),
      JSON.stringify({ ...session, parts: [...session.parts, null] }),
      JSON.stringify({ ...session, steps: [{ kind: 'focus', partId: 'maths', minutes: 10 }] }),
      JSON.stringify({ ...session, steps: [{ kind: 'break', minutes: '2' }] }),
      JSON.stringify({
        ...session,
        steps: [{ kind: 'nap', partId: 'maths', index: 1, of: 1, minutes: 10 }],
      }),
      JSON.stringify({
        ...session,
        steps: [{ kind: 'focus', partId: 'maths', of: 1, minutes: 10 }],
      }),
      JSON.stringify({
        ...session,
        steps: [{ kind: 'focus', partId: 'maths', index: 1, minutes: 10 }],
      }),
      JSON.stringify({ ...session, steps: [...session.steps, { kind: 'nap', minutes: 3 }] }),
      JSON.stringify({
        ...session,
        steps: [{ kind: 'focus', partId: 'history', index: 1, of: 1, minutes: 10 }],
      }),
    ]) {
      localStorage.setItem(KEY, junk);
      expect(readHomeworkSession('2026-10-14')).toBeNull();
    }
  });

  it('clears the stored session', () => {
    writeHomeworkSession(session);
    clearHomeworkSession();

    expect(localStorage.getItem(KEY)).toBeNull();
  });

  it('survives storage that is unavailable', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blocked');
    });
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked');
    });
    vi.spyOn(Storage.prototype, 'removeItem').mockImplementation(() => {
      throw new Error('blocked');
    });

    expect(() => writeHomeworkSession(session)).not.toThrow();
    expect(() => clearHomeworkSession()).not.toThrow();
    expect(readHomeworkSession('2026-10-14')).toBeNull();
  });
});

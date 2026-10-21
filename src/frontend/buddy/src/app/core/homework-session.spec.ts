import { HomeworkPart } from './homework-plan';
import {
  HomeworkSession,
  addChunk,
  breaksBefore,
  createSession,
  currentStep,
  finishPart,
  homeworkTimerState,
  isFinished,
  next,
  pause,
  resume,
  start,
} from './homework-session';

const MIN = 60_000;
const T0 = 1_000_000;
const CHUNKS = { focusMinutes: 10, breakMinutes: 2 };

const parts: HomeworkPart[] = [
  { id: 'maths', title: 'Maths', estimateMinutes: 20 },
  { id: 'reading', title: 'Reading', estimateMinutes: 10 },
];

// maths 1/2, break, maths 2/2, break, reading 1/1
const session = (): HomeworkSession => createSession(parts, CHUNKS, '2026-10-14');

describe('homework session', () => {
  it('starts on the first focus chunk, not yet running', () => {
    const s = session();

    expect(s.steps).toHaveLength(5);
    expect(currentStep(s)).toEqual({
      kind: 'focus',
      partId: 'maths',
      index: 1,
      of: 2,
      minutes: 10,
    });
    expect(homeworkTimerState(s, T0)).toMatchObject({
      phase: 'upcoming',
      remainingMs: 10 * MIN,
      budgetMs: 10 * MIN,
      runRemainingMs: 34 * MIN,
      runBudgetMs: 34 * MIN,
      deadlineMs: null,
      childStarted: false,
    });
  });

  it('counts down once started and reads as time is up at the deadline', () => {
    const s = start(session(), T0);

    expect(homeworkTimerState(s, T0 + 4 * MIN)).toMatchObject({
      phase: 'running',
      remainingMs: 6 * MIN,
      runRemainingMs: 30 * MIN,
      deadlineMs: T0 + 10 * MIN,
      childStarted: true,
    });
    expect(homeworkTimerState(s, T0 + 10 * MIN)).toMatchObject({
      phase: 'timesUp',
      remainingMs: 0,
      deadlineMs: null,
    });
    expect(homeworkTimerState(s, T0 + 60 * MIN).remainingMs).toBe(0);
  });

  it('ignores a second start', () => {
    const s = start(session(), T0);
    expect(start(s, T0 + MIN)).toBe(s);
  });

  it('keeps the time left across a pause', () => {
    const paused = pause(start(session(), T0), T0 + 3 * MIN);

    expect(homeworkTimerState(paused, T0 + 20 * MIN)).toMatchObject({
      phase: 'paused',
      remainingMs: 7 * MIN,
      deadlineMs: null,
    });

    const resumed = resume(paused, T0 + 20 * MIN);
    expect(resumed.pausedAtMs).toBeNull();
    expect(homeworkTimerState(resumed, T0 + 21 * MIN)).toMatchObject({
      phase: 'running',
      remainingMs: 6 * MIN,
      deadlineMs: T0 + 27 * MIN,
    });
  });

  it('leaves pause and resume alone when they do not apply', () => {
    const fresh = session();
    expect(pause(fresh, T0)).toBe(fresh);
    expect(resume(fresh, T0)).toBe(fresh);

    const running = start(fresh, T0);
    expect(resume(running, T0)).toBe(running);

    const paused = pause(running, T0 + MIN);
    expect(pause(paused, T0 + 2 * MIN)).toBe(paused);
  });

  it('moves on step by step, starting each new step at the tap', () => {
    let s = start(session(), T0);
    s = next(s, T0 + 8 * MIN);

    expect(currentStep(s)).toEqual({ kind: 'break', minutes: 2 });
    expect(s.startedAtMs).toBe(T0 + 8 * MIN);
    expect(breaksBefore(s)).toBe(0);

    s = next(pause(s, T0 + 9 * MIN), T0 + 9 * MIN);
    expect(currentStep(s)).toMatchObject({ partId: 'maths', index: 2 });
    expect(s.pausedAtMs).toBeNull();
    expect(breaksBefore(s)).toBe(1);

    s = next(next(next(s, T0), T0), T0);
    expect(isFinished(s)).toBe(true);
    expect(s.startedAtMs).toBeNull();
    expect(currentStep(s)).toBeNull();
    expect(homeworkTimerState(s, T0)).toMatchObject({
      phase: 'done',
      remainingMs: 0,
      childStarted: false,
    });
    expect(next(s, T0).currentIndex).toBe(s.steps.length);
  });

  it('drops the rest of a part, and the breaks before them, when the part is done early', () => {
    const s = finishPart(start(session(), T0), T0 + 5 * MIN);

    expect(s.steps).toEqual([
      { kind: 'focus', partId: 'maths', index: 1, of: 1, minutes: 10 },
      { kind: 'break', minutes: 2 },
      { kind: 'focus', partId: 'reading', index: 1, of: 1, minutes: 10 },
    ]);
    expect(currentStep(s)).toEqual({ kind: 'break', minutes: 2 });
    expect(s.startedAtMs).toBe(T0 + 5 * MIN);
  });

  it('finishes the session when the last part is done early', () => {
    let s = start(session(), T0);
    s = next(next(next(next(s, T0), T0), T0), T0);
    expect(currentStep(s)).toMatchObject({ partId: 'reading' });

    s = finishPart(s, T0);
    expect(isFinished(s)).toBe(true);
  });

  it('drops the break after the previous part when the last part is done early', () => {
    const reversed = createSession([parts[1], parts[0]], CHUNKS, '2026-10-14');
    // reading 1/1, break, maths 1/2, break, maths 2/2
    let s = next(next(start(reversed, T0), T0), T0);
    expect(currentStep(s)).toMatchObject({ partId: 'maths', index: 1 });

    s = finishPart(s, T0);
    expect(s.steps).toEqual([
      { kind: 'focus', partId: 'reading', index: 1, of: 1, minutes: 10 },
      { kind: 'break', minutes: 2 },
      { kind: 'focus', partId: 'maths', index: 1, of: 1, minutes: 10 },
    ]);
    expect(isFinished(s)).toBe(true);
  });

  it('keeps another part that sits between the chunks of the finished part', () => {
    const s: HomeworkSession = {
      ...session(),
      steps: [
        { kind: 'focus', partId: 'maths', index: 1, of: 2, minutes: 10 },
        { kind: 'focus', partId: 'reading', index: 1, of: 1, minutes: 10 },
        { kind: 'focus', partId: 'maths', index: 2, of: 2, minutes: 10 },
      ],
    };

    expect(finishPart(s, T0).steps).toEqual([
      { kind: 'focus', partId: 'maths', index: 1, of: 1, minutes: 10 },
      { kind: 'focus', partId: 'reading', index: 1, of: 1, minutes: 10 },
    ]);
  });

  it('adds one more chunk of the focus length, started at once with no break', () => {
    const plan: HomeworkPart[] = [{ id: 'maths', title: 'Maths', estimateMinutes: 15 }];
    let s = start(createSession(plan, CHUNKS, '2026-10-14'), T0); // 8 + 7
    s = addChunk(s, T0 + 9 * MIN);

    expect(s.steps).toEqual([
      { kind: 'focus', partId: 'maths', index: 1, of: 3, minutes: 8 },
      { kind: 'focus', partId: 'maths', index: 2, of: 3, minutes: 10 },
      { kind: 'break', minutes: 2 },
      { kind: 'focus', partId: 'maths', index: 3, of: 3, minutes: 7 },
    ]);
    expect(s.currentIndex).toBe(1);
    expect(s.startedAtMs).toBe(T0 + 9 * MIN);
  });

  it('only finishes a part or adds a chunk during a focus step', () => {
    const onBreak = next(start(session(), T0), T0);
    expect(finishPart(onBreak, T0)).toBe(onBreak);
    expect(addChunk(onBreak, T0)).toBe(onBreak);
  });
});

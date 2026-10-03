import { describe, expect, it } from 'vitest';

import { createAction } from './action-state';

describe('createAction', () => {
  it('starts idle', () => {
    const action = createAction<string>();

    expect(action.state()).toEqual({ status: 'idle' });
    expect(action.busy()).toBe(false);
  });

  it('is busy for the running id only, then idle once the work succeeds', async () => {
    const action = createAction<string>();
    let finish: () => void = () => undefined;
    const work = new Promise<void>((resolve) => (finish = resolve));

    const running = action.run('a', () => work, 'x.error');

    expect(action.state()).toEqual({ status: 'busy', id: 'a' });
    expect(action.busy()).toBe(true);
    expect(action.isBusy('a')).toBe(true);
    expect(action.isBusy('b')).toBe(false);

    finish();

    await expect(running).resolves.toBe(true);
    expect(action.state()).toEqual({ status: 'idle' });
  });

  it('fails with the given message when the work throws', async () => {
    const action = createAction<string>();

    const succeeded = await action.run('a', () => Promise.reject(new Error('boom')), 'x.error');

    expect(succeeded).toBe(false);
    expect(action.state()).toEqual({ status: 'error', id: 'a', message: 'x.error' });
    expect(action.isBusy('a')).toBe(false);
  });

  it('derives the message from the thrown error when given a function', async () => {
    const action = createAction<string>();

    await action.run(
      'a',
      () => Promise.reject(new Error('Bad key')),
      (error) => (error instanceof Error ? error.message : 'x.error'),
    );

    expect(action.state()).toEqual({ status: 'error', id: 'a', message: 'Bad key' });
  });

  it('can fail without running, and reset back to idle', () => {
    const action = createAction<string>();

    action.fail('a', 'x.invalid');
    expect(action.state()).toEqual({ status: 'error', id: 'a', message: 'x.invalid' });

    action.reset();
    expect(action.state()).toEqual({ status: 'idle' });
  });

  it('defaults to a single-target action keyed by true', async () => {
    const action = createAction();

    await action.run(true, () => Promise.resolve(), 'x.error');

    expect(action.state()).toEqual({ status: 'idle' });
  });

  it('keeps an earlier failure when a later overlapping run succeeds', async () => {
    const action = createAction<string>();
    let failA: () => void = () => undefined;
    let finishB: () => void = () => undefined;

    const a = action.run(
      'a',
      () => new Promise<void>((_, reject) => (failA = () => reject(new Error()))),
      'a.error',
    );
    const b = action.run('b', () => new Promise<void>((resolve) => (finishB = resolve)), 'b.error');

    failA();
    await a;
    finishB();
    await b;

    expect(action.state()).toEqual({ status: 'error', id: 'a', message: 'a.error' });
  });

  it('lets a later run keep its busy state when an earlier one succeeds', async () => {
    const action = createAction<string>();
    let finishA: () => void = () => undefined;

    const a = action.run('a', () => new Promise<void>((resolve) => (finishA = resolve)), 'a.error');
    void action.run('b', () => new Promise<void>(() => undefined), 'b.error');

    finishA();
    await a;

    expect(action.isBusy('b')).toBe(true);
  });

  it('clears only an error, never a busy run', () => {
    const action = createAction<string>();

    void action.run('a', () => new Promise<void>(() => undefined), 'a.error');
    action.clearError();
    expect(action.isBusy('a')).toBe(true);

    action.fail('a', 'a.error');
    action.clearError();
    expect(action.state()).toEqual({ status: 'idle' });
  });
});

import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { keepTabInside } from './focus-trap';

describe('keepTabInside', () => {
  let panel: HTMLElement;
  let first: HTMLButtonElement;
  let middle: HTMLInputElement;
  let last: HTMLButtonElement;
  let outside: HTMLButtonElement;

  beforeEach(() => {
    panel = document.createElement('div');
    panel.tabIndex = -1;
    first = document.createElement('button');
    middle = document.createElement('input');
    last = document.createElement('button');
    const disabled = document.createElement('button');
    disabled.disabled = true;
    panel.append(first, middle, last, disabled);
    outside = document.createElement('button');
    document.body.append(panel, outside);
  });

  afterEach(() => {
    panel.remove();
    outside.remove();
  });

  function tab(shiftKey = false): KeyboardEvent {
    const event = new KeyboardEvent('keydown', { key: 'Tab', shiftKey, cancelable: true });
    keepTabInside(panel, event);
    return event;
  }

  it('wraps Tab from the last enabled control to the first, and Shift+Tab the other way', () => {
    last.focus();
    expect(tab().defaultPrevented).toBe(true);
    expect(document.activeElement).toBe(first);

    first.focus();
    expect(tab(true).defaultPrevented).toBe(true);
    expect(document.activeElement).toBe(last);
  });

  it('leaves Tab between inner controls to the browser', () => {
    middle.focus();
    expect(tab().defaultPrevented).toBe(false);
    expect(tab(true).defaultPrevented).toBe(false);
  });

  it('pulls focus back in from outside the panel', () => {
    outside.focus();
    expect(tab().defaultPrevented).toBe(true);
    expect(document.activeElement).toBe(first);
  });

  it('falls back to the panel without focusable controls, and ignores other events', () => {
    panel.replaceChildren();
    outside.focus();
    tab();
    expect(document.activeElement).toBe(panel);

    keepTabInside(undefined, new KeyboardEvent('keydown', { key: 'Tab' }));
    const notKeyboard = new Event('keydown', { cancelable: true });
    keepTabInside(panel, notKeyboard);
    expect(notKeyboard.defaultPrevented).toBe(false);
  });
});

import { describe, expect, it } from 'vitest';

import { applyMarkdownAction } from './markdown-edits';

describe('applyMarkdownAction', () => {
  it('wraps the selection in bold markers and keeps it selected', () => {
    expect(applyMarkdownAction('bold', 'say never here', { start: 4, end: 9 })).toEqual({
      text: 'say **never** here',
      selection: { start: 6, end: 11 },
    });
  });

  it('puts the cursor between empty bold markers when nothing is selected', () => {
    expect(applyMarkdownAction('bold', 'ab', { start: 1, end: 1 })).toEqual({
      text: 'a****b',
      selection: { start: 3, end: 3 },
    });
  });

  it('makes a link from the selection with the URL selected', () => {
    expect(applyMarkdownAction('link', 'see school', { start: 4, end: 10 })).toEqual({
      text: 'see [school](https://)',
      selection: { start: 13, end: 21 },
    });
  });

  it('prefixes every selected line for a bulleted list', () => {
    expect(applyMarkdownAction('bulletList', 'intro\none\ntwo', { start: 7, end: 12 })).toEqual({
      text: 'intro\n- one\n- two',
      selection: { start: 8, end: 17 },
    });
  });

  it('numbers the selected lines', () => {
    expect(applyMarkdownAction('numberedList', 'one\ntwo', { start: 0, end: 7 }).text).toBe(
      '1. one\n2. two',
    );
  });

  it('turns the cursor line into a checklist item', () => {
    expect(applyMarkdownAction('checkList', 'a\nhomework\nb', { start: 4, end: 4 }).text).toBe(
      'a\n- [ ] homework\nb',
    );
  });

  it('inserts a table on its own lines, separated by blank lines', () => {
    const edit = applyMarkdownAction('table', 'above\nbelow', { start: 5, end: 5 });

    expect(edit.text).toBe('above\n\n| Day | Time |\n| --- | --- |\n|  |  |\n\nbelow');
    expect(edit.text.slice(edit.selection.start, edit.selection.end)).toBe(
      '| Day | Time |\n| --- | --- |\n|  |  |',
    );
  });

  it('inserts a table into an empty body without blank lines around it', () => {
    expect(applyMarkdownAction('table', '', { start: 0, end: 0 }).text).toBe(
      '| Day | Time |\n| --- | --- |\n|  |  |',
    );
  });

  it('uses the table headers it is given', () => {
    expect(applyMarkdownAction('table', '', { start: 0, end: 0 }, ['Dag', 'Tid']).text).toBe(
      '| Dag | Tid |\n| --- | --- |\n|  |  |',
    );
  });
});

// The markdown-editor toolbar's edits, as pure functions of the text and the selection so they can
// be tested without a DOM. Each returns the new text and the selection to restore.

export interface TextSelection {
  start: number;
  end: number;
}

export interface TextEdit {
  text: string;
  selection: TextSelection;
}

export type MarkdownAction =
  'bold' | 'bulletList' | 'numberedList' | 'checkList' | 'table' | 'link';

// The table skeleton's two column headers, translated by the caller.
export type TableHeaders = readonly [string, string];

export function applyMarkdownAction(
  action: MarkdownAction,
  text: string,
  selection: TextSelection,
  tableHeaders: TableHeaders = ['Day', 'Time'],
): TextEdit {
  switch (action) {
    case 'bold':
      return wrap(text, selection, '**', '**');
    case 'link':
      return link(text, selection);
    case 'bulletList':
      return prefixLines(text, selection, () => '- ');
    case 'numberedList':
      return prefixLines(text, selection, (index) => `${index + 1}. `);
    case 'checkList':
      return prefixLines(text, selection, () => '- [ ] ');
    case 'table':
      return insertBlock(
        text,
        selection,
        `| ${tableHeaders[0]} | ${tableHeaders[1]} |\n| --- | --- |\n|  |  |`,
      );
  }
}

// **selected**; with nothing selected the cursor lands between the markers.
function wrap(
  text: string,
  { start, end }: TextSelection,
  before: string,
  after: string,
): TextEdit {
  const selected = text.slice(start, end);

  return {
    text: text.slice(0, start) + before + selected + after + text.slice(end),
    selection: { start: start + before.length, end: end + before.length },
  };
}

// [selected](https://) with the URL selected, ready to be typed over.
function link(text: string, { start, end }: TextSelection): TextEdit {
  const label = text.slice(start, end);
  const url = 'https://';
  const inserted = `[${label}](${url})`;
  const urlStart = start + label.length + 3;

  return {
    text: text.slice(0, start) + inserted + text.slice(end),
    selection: { start: urlStart, end: urlStart + url.length },
  };
}

// Prefixes every line the selection touches (or the cursor's line).
function prefixLines(
  text: string,
  { start, end }: TextSelection,
  prefix: (index: number) => string,
): TextEdit {
  const lineStart = text.lastIndexOf('\n', start - 1) + 1;
  const nextBreak = text.indexOf('\n', end);
  const lineEnd = nextBreak === -1 ? text.length : nextBreak;
  const lines = text.slice(lineStart, lineEnd).split('\n');
  const changed = lines.map((line, index) => prefix(index) + line).join('\n');

  return {
    text: text.slice(0, lineStart) + changed + text.slice(lineEnd),
    selection: { start: lineStart + prefix(0).length, end: lineStart + changed.length },
  };
}

// A block on lines of its own, separated from the text around it by a blank line.
function insertBlock(text: string, { start, end }: TextSelection, block: string): TextEdit {
  const before = text.slice(0, start);
  const after = text.slice(end);
  const lead =
    before === '' || before.endsWith('\n\n') ? '' : before.endsWith('\n') ? '\n' : '\n\n';
  const trail =
    after === '' || after.startsWith('\n\n') ? '' : after.startsWith('\n') ? '\n' : '\n\n';
  const blockStart = before.length + lead.length;

  return {
    text: before + lead + block + trail + after,
    selection: { start: blockStart, end: blockStart + block.length },
  };
}

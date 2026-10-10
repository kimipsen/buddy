import { marked, type Token, type Tokens } from 'marked';

// The allow-listed markdown subset the app renders (docs/backend/analysis/house-rules.md,
// Question 4). marked's lexer turns the guardian's text into a token tree; this maps it onto a small
// node model that the markdown-view template renders with plain Angular bindings. marked's HTML
// renderer is never called and nothing goes through innerHTML, so there is nothing to sanitize: a
// token outside the allow list (raw HTML, images, links with any other scheme) becomes its own
// source text, so the guardian sees exactly what they typed.

export type MdAlign = 'left' | 'center' | 'right' | null;

export type MdInline =
  | { kind: 'text'; text: string }
  | { kind: 'strong' | 'em' | 'del'; children: MdInline[] }
  | { kind: 'code'; text: string }
  | { kind: 'br' }
  | { kind: 'link'; href: string; children: MdInline[] };

export interface MdCell {
  align: MdAlign;
  inlines: MdInline[];
}

export interface MdListItem {
  // null for a plain bullet; true/false for a GFM task-list item, rendered read-only.
  checked: boolean | null;
  blocks: MdBlock[];
}

export type MdBlock =
  | { kind: 'heading'; level: 3 | 4 | 5 | 6; inlines: MdInline[] }
  | { kind: 'paragraph'; inlines: MdInline[] }
  // Tight list items hold their text without a paragraph around it.
  | { kind: 'inline'; inlines: MdInline[] }
  | { kind: 'list'; ordered: boolean; start: number; items: MdListItem[] }
  | { kind: 'blockquote'; blocks: MdBlock[] }
  | { kind: 'table'; header: MdCell[]; rows: MdCell[][] }
  | { kind: 'code'; text: string }
  | { kind: 'hr' };

const ALLOWED_LINK = /^(https?:|mailto:)/i;

export function parseMarkdown(markdown: string): MdBlock[] {
  return toBlocks(marked.lexer(markdown, { gfm: true }));
}

export function isAllowedHref(href: string): boolean {
  return ALLOWED_LINK.test(href.trim());
}

function toBlocks(tokens: Token[]): MdBlock[] {
  return tokens.flatMap((token) => toBlock(token));
}

function toBlock(token: Token): MdBlock[] {
  switch (token.type) {
    case 'heading': {
      const heading = token as Tokens.Heading;
      // # and ## drop to ### so they don't compete with the rule's own title.
      const level = Math.min(Math.max(heading.depth, 3), 6) as 3 | 4 | 5 | 6;
      return [{ kind: 'heading', level, inlines: toInlines(heading.tokens) }];
    }
    case 'paragraph':
      return [{ kind: 'paragraph', inlines: toInlines((token as Tokens.Paragraph).tokens) }];
    case 'text': {
      const text = token as Tokens.Text;
      return [
        { kind: 'inline', inlines: text.tokens ? toInlines(text.tokens) : [plain(text.text)] },
      ];
    }
    case 'list': {
      const list = token as Tokens.List;
      return [
        {
          kind: 'list',
          ordered: list.ordered,
          start: typeof list.start === 'number' ? list.start : 1,
          items: list.items.map(toListItem),
        },
      ];
    }
    case 'blockquote':
      return [{ kind: 'blockquote', blocks: toBlocks((token as Tokens.Blockquote).tokens) }];
    case 'table': {
      const table = token as Tokens.Table;
      return [
        {
          kind: 'table',
          header: table.header.map(toCell),
          rows: table.rows.map((row) => row.map(toCell)),
        },
      ];
    }
    case 'code':
      return [{ kind: 'code', text: (token as Tokens.Code).text }];
    case 'hr':
      return [{ kind: 'hr' }];
    // Blank lines and reference-link definitions (their links are already resolved) show nothing.
    case 'space':
    case 'def':
      return [];
    // Raw HTML and anything marked adds later: the source, as text.
    default:
      return token.raw.trim() === '' ? [] : [{ kind: 'paragraph', inlines: [plain(token.raw)] }];
  }
}

function toListItem(item: Tokens.ListItem): MdListItem {
  // In a tight list marked puts the task-list checkbox in front of the item's content (in a loose
  // one, inside its paragraph -- see toInline); it's carried by `checked`.
  const content = item.tokens.filter((token) => token.type !== 'checkbox');
  return { checked: item.task ? item.checked === true : null, blocks: toBlocks(content) };
}

function toCell(cell: Tokens.TableCell): MdCell {
  return { align: cell.align, inlines: toInlines(cell.tokens) };
}

function toInlines(tokens: Token[]): MdInline[] {
  return tokens.flatMap((token) => toInline(token));
}

function toInline(token: Token): MdInline[] {
  switch (token.type) {
    case 'text': {
      const text = token as Tokens.Text;
      return text.tokens ? toInlines(text.tokens) : [plain(text.text)];
    }
    case 'escape':
      return [plain((token as Tokens.Escape).text)];
    case 'strong':
    case 'em':
    case 'del':
      return [
        {
          kind: token.type,
          children: toInlines((token as Tokens.Strong | Tokens.Em | Tokens.Del).tokens),
        },
      ];
    case 'codespan':
      return [{ kind: 'code', text: (token as Tokens.Codespan).text }];
    case 'br':
      return [{ kind: 'br' }];
    // In a loose task list marked puts the checkbox inside the item's paragraph; MdListItem.checked
    // already carries it.
    case 'checkbox':
      return [];
    case 'link': {
      const link = token as Tokens.Link;
      return isAllowedHref(link.href)
        ? [{ kind: 'link', href: link.href.trim(), children: toInlines(link.tokens) }]
        : [plain(link.raw)];
    }
    // Images (no tracking pixels; the CSP allows only same-origin images anyway), inline HTML and
    // anything unknown: the source, as text.
    default:
      return [plain(token.raw)];
  }
}

function plain(text: string): MdInline {
  return { kind: 'text', text };
}

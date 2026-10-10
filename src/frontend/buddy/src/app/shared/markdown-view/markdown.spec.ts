import { describe, expect, it } from 'vitest';

import { isAllowedHref, parseMarkdown } from './markdown';

describe('parseMarkdown', () => {
  it('drops # and ## headings to level 3 and keeps deeper levels', () => {
    expect(parseMarkdown('# One\n\n## Two\n\n#### Four\n\n###### Six')).toEqual([
      { kind: 'heading', level: 3, inlines: [{ kind: 'text', text: 'One' }] },
      { kind: 'heading', level: 3, inlines: [{ kind: 'text', text: 'Two' }] },
      { kind: 'heading', level: 4, inlines: [{ kind: 'text', text: 'Four' }] },
      { kind: 'heading', level: 6, inlines: [{ kind: 'text', text: 'Six' }] },
    ]);
  });

  it('maps emphasis, strikethrough, inline code and hard line breaks', () => {
    expect(parseMarkdown('**b** *i* ~~d~~ `c`  \nnext')).toEqual([
      {
        kind: 'paragraph',
        inlines: [
          { kind: 'strong', children: [{ kind: 'text', text: 'b' }] },
          { kind: 'text', text: ' ' },
          { kind: 'em', children: [{ kind: 'text', text: 'i' }] },
          { kind: 'text', text: ' ' },
          { kind: 'del', children: [{ kind: 'text', text: 'd' }] },
          { kind: 'text', text: ' ' },
          { kind: 'code', text: 'c' },
          { kind: 'br' },
          { kind: 'text', text: 'next' },
        ],
      },
    ]);
  });

  it('maps nested, numbered and task lists', () => {
    expect(parseMarkdown('3. three\n4. four\n\n- [x] done\n- [ ] open\n  - nested')).toEqual([
      {
        kind: 'list',
        ordered: true,
        start: 3,
        items: [
          {
            checked: null,
            blocks: [{ kind: 'inline', inlines: [{ kind: 'text', text: 'three' }] }],
          },
          {
            checked: null,
            blocks: [{ kind: 'inline', inlines: [{ kind: 'text', text: 'four' }] }],
          },
        ],
      },
      {
        kind: 'list',
        ordered: false,
        start: 1,
        items: [
          {
            checked: true,
            blocks: [{ kind: 'inline', inlines: [{ kind: 'text', text: 'done' }] }],
          },
          {
            checked: false,
            blocks: [
              { kind: 'inline', inlines: [{ kind: 'text', text: 'open' }] },
              {
                kind: 'list',
                ordered: false,
                start: 1,
                items: [
                  {
                    checked: null,
                    blocks: [{ kind: 'inline', inlines: [{ kind: 'text', text: 'nested' }] }],
                  },
                ],
              },
            ],
          },
        ],
      },
    ]);
  });

  it('shows the checkbox of a loose task list once, as the item state', () => {
    expect(parseMarkdown('- [x] done\n\n- [ ] open')).toEqual([
      {
        kind: 'list',
        ordered: false,
        start: 1,
        items: [
          {
            checked: true,
            blocks: [{ kind: 'paragraph', inlines: [{ kind: 'text', text: 'done' }] }],
          },
          {
            checked: false,
            blocks: [{ kind: 'paragraph', inlines: [{ kind: 'text', text: 'open' }] }],
          },
        ],
      },
    ]);
  });

  it('maps a GFM table with column alignment', () => {
    expect(parseMarkdown('| Day | Time |\n|:---|---:|\n| Mon | **45** |')).toEqual([
      {
        kind: 'table',
        header: [
          { align: 'left', inlines: [{ kind: 'text', text: 'Day' }] },
          { align: 'right', inlines: [{ kind: 'text', text: 'Time' }] },
        ],
        rows: [
          [
            { align: 'left', inlines: [{ kind: 'text', text: 'Mon' }] },
            {
              align: 'right',
              inlines: [{ kind: 'strong', children: [{ kind: 'text', text: '45' }] }],
            },
          ],
        ],
      },
    ]);
  });

  it('maps block quotes, rules and code blocks', () => {
    expect(parseMarkdown('> quoted\n\n---\n\n```\n<b>x</b>\n```')).toEqual([
      {
        kind: 'blockquote',
        blocks: [{ kind: 'paragraph', inlines: [{ kind: 'text', text: 'quoted' }] }],
      },
      { kind: 'hr' },
      { kind: 'code', text: '<b>x</b>' },
    ]);
  });

  it('keeps http, https and mailto links, including reference-style ones', () => {
    expect(parseMarkdown('[a](https://a.dk) [m](mailto:x@y.dk) [r]\n\n[r]: http://r.dk')).toEqual([
      {
        kind: 'paragraph',
        inlines: [
          { kind: 'link', href: 'https://a.dk', children: [{ kind: 'text', text: 'a' }] },
          { kind: 'text', text: ' ' },
          { kind: 'link', href: 'mailto:x@y.dk', children: [{ kind: 'text', text: 'm' }] },
          { kind: 'text', text: ' ' },
          { kind: 'link', href: 'http://r.dk', children: [{ kind: 'text', text: 'r' }] },
        ],
      },
    ]);
  });

  it('turns links with other schemes, images and raw HTML into their source text', () => {
    expect(
      parseMarkdown(
        '[x](javascript:alert(1)) ![i](https://t.dk/p.png) <b>x</b>\n\n<script>alert(1)</script>',
      ),
    ).toEqual([
      {
        kind: 'paragraph',
        inlines: [
          { kind: 'text', text: '[x](javascript:alert(1))' },
          { kind: 'text', text: ' ' },
          { kind: 'text', text: '![i](https://t.dk/p.png)' },
          { kind: 'text', text: ' ' },
          { kind: 'text', text: '<b>' },
          { kind: 'text', text: 'x' },
          { kind: 'text', text: '</b>' },
        ],
      },
      { kind: 'paragraph', inlines: [{ kind: 'text', text: '<script>alert(1)</script>' }] },
    ]);
  });

  it('turns a backslash escape into the escaped character', () => {
    expect(parseMarkdown('\\*not bold\\*')).toEqual([
      {
        kind: 'paragraph',
        inlines: [
          { kind: 'text', text: '*' },
          { kind: 'text', text: 'not bold' },
          { kind: 'text', text: '*' },
        ],
      },
    ]);
  });

  it('renders nothing for an empty body', () => {
    expect(parseMarkdown('')).toEqual([]);
  });
});

describe('isAllowedHref', () => {
  it.each([
    ['https://buddy.dk', true],
    ['HTTP://buddy.dk', true],
    ['  mailto:a@b.dk', true],
    ['javascript:alert(1)', false],
    ['JaVaScRiPt:alert(1)', false],
    ['data:text/html,<script>', false],
    ['vbscript:x', false],
    ['/relative', false],
    ['//evil.example', false],
  ])('%s -> %s', (href, allowed) => {
    expect(isAllowedHref(href)).toBe(allowed);
  });
});

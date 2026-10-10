import { Sanitizer, SecurityContext } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';

import { MarkdownView } from './markdown-view';

describe('MarkdownView', () => {
  async function render(markdown: string): Promise<HTMLElement> {
    await TestBed.configureTestingModule({ imports: [MarkdownView] }).compileComponents();

    const fixture = TestBed.createComponent(MarkdownView);
    fixture.componentRef.setInput('markdown', markdown);
    fixture.detectChanges();

    return fixture.nativeElement as HTMLElement;
  }

  it('renders headings below the rule title level', async () => {
    const compiled = await render('# Big\n\n#### Small');

    expect(compiled.querySelector('h1, h2')).toBeNull();
    expect(compiled.querySelector('h3')?.textContent?.trim()).toBe('Big');
    expect(compiled.querySelector('h4')?.textContent?.trim()).toBe('Small');
  });

  it('renders inline formatting with the matching elements', async () => {
    const compiled = await render('**bold** *it* ~~gone~~ `code`');

    expect(compiled.querySelector('p strong')?.textContent).toBe('bold');
    expect(compiled.querySelector('p em')?.textContent).toBe('it');
    expect(compiled.querySelector('p del')?.textContent).toBe('gone');
    expect(compiled.querySelector('p code')?.textContent).toBe('code');
  });

  it('adds no whitespace around formatted text', async () => {
    const compiled = await render('Before **bold**, then [link](https://a.dk).');

    expect(compiled.querySelector('p')?.textContent).toBe('Before bold, then link.');
  });

  it('renders bulleted, numbered and nested lists', async () => {
    const compiled = await render('- a\n  - a1\n- b\n\n5. five');

    expect(compiled.querySelectorAll('ul > li')).toHaveLength(3);
    expect(compiled.querySelector('ul li ul li')?.textContent?.trim()).toBe('a1');
    expect(compiled.querySelector('ol')?.getAttribute('start')).toBe('5');
  });

  it('renders task lists read-only with an accessible state', async () => {
    const compiled = await render('- [x] Homework\n- [ ] Tablet on the charger');

    const items = compiled.querySelectorAll('li');
    expect(items[0].querySelector('[data-task]')?.getAttribute('data-task')).toBe('done');
    expect(items[0].querySelector('.sr-only')?.textContent?.trim()).toBe('Done');
    expect(items[1].querySelector('[data-task]')?.getAttribute('data-task')).toBe('open');
    expect(items[1].querySelector('.sr-only')?.textContent?.trim()).toBe('Not done');
    expect(compiled.querySelector('input')).toBeNull();
  });

  it('renders a table with header cells and column alignment', async () => {
    const compiled = await render(
      '| Day | Time | When |\n|:---|---:|:---:|\n| Mon--Thu | 45 min | after homework |',
    );

    const headers = compiled.querySelectorAll<HTMLTableCellElement>('thead th');
    expect([...headers].map((th) => th.textContent?.trim())).toEqual(['Day', 'Time', 'When']);
    expect(headers[0].getAttribute('scope')).toBe('col');

    const cells = compiled.querySelectorAll<HTMLTableCellElement>('tbody td');
    expect([...cells].map((td) => td.textContent?.trim())).toEqual([
      'Mon--Thu',
      '45 min',
      'after homework',
    ]);
    expect([...cells].map((td) => td.style.textAlign)).toEqual(['left', 'right', 'center']);
    // Narrow screens scroll the table, not the page.
    expect(compiled.querySelector('table')?.parentElement?.classList).toContain('overflow-x-auto');
  });

  it('opens allowed links in a new tab without an opener', async () => {
    const compiled = await render('[Skolen](https://skolen.dk) and [mail](mailto:a@b.dk)');

    const links = compiled.querySelectorAll('a');
    expect([...links].map((a) => a.getAttribute('href'))).toEqual([
      'https://skolen.dk',
      'mailto:a@b.dk',
    ]);
    expect(links[0].getAttribute('target')).toBe('_blank');
    expect(links[0].getAttribute('rel')).toBe('noopener noreferrer');
  });

  it('renders block quotes, horizontal rules and code blocks', async () => {
    const compiled = await render('> Ask first\n\n---\n\n```\nline\n```');

    expect(compiled.querySelector('blockquote')?.textContent?.trim()).toBe('Ask first');
    expect(compiled.querySelector('hr')).not.toBeNull();
    expect(compiled.querySelector('pre code')?.textContent).toBe('line');
  });

  it.each([
    ['<script>alert(1)</script>', 'script'],
    ['<img src=x onerror="alert(1)">', 'img'],
    ['<iframe src="https://evil.example"></iframe>', 'iframe'],
    ['Hi <b onmouseover="alert(1)">there</b>', 'b'],
    ['![pixel](https://tracker.example/p.png)', 'img'],
  ])('shows %s as literal text and creates no %s element', async (markdown, tag) => {
    const compiled = await render(markdown);

    expect(compiled.querySelector(tag)).toBeNull();
    expect(compiled.textContent?.replace(/\s+/g, ' ')).toContain(markdown.replace(/\s+/g, ' '));
    expect(
      [...compiled.querySelectorAll('*')].flatMap((el) => el.getAttributeNames()),
    ).not.toContain('onerror');
  });

  it('shows a javascript: link as its source text instead of a link', async () => {
    const compiled = await render('[click me](javascript:alert(1))');

    expect(compiled.querySelector('a')).toBeNull();
    expect(compiled.textContent).toContain('[click me](javascript:alert(1))');
  });

  it('never hands the sanitizer HTML, so nothing is rendered through innerHTML', async () => {
    // Angular routes every [innerHTML]/[outerHTML] binding through the Sanitizer with the HTML
    // context -- in nested templates too. Rendering every supported construct plus raw HTML must
    // only ever ask it about URLs (the links' href).
    const contexts: SecurityContext[] = [];
    TestBed.configureTestingModule({
      providers: [
        {
          provide: Sanitizer,
          useValue: {
            sanitize: (context: SecurityContext, value: unknown) => {
              contexts.push(context);
              return value === null ? null : String(value);
            },
          },
        },
      ],
    });

    const compiled = await render(
      '# H\n\n**b** *i* ~~d~~ `c` [a](https://a.dk)\n\n- [x] t\n  - n\n\n> q\n\n| a |\n|---|\n| 1 |\n\n<b>x</b>',
    );

    expect(compiled.querySelector('table')).not.toBeNull();
    expect(contexts).not.toContain(SecurityContext.HTML);
    expect(contexts).toContain(SecurityContext.URL);
  });

  it('renders nothing for an empty body', async () => {
    const compiled = await render('');

    expect(compiled.textContent?.trim()).toBe('');
  });
});

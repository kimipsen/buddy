import { DOCUMENT, DestroyRef, inject } from '@angular/core';

export interface PrintPage {
  size: 'A3' | 'A4';
  orientation: 'portrait' | 'landscape';
  margin: string;
}

export interface PrintPageStyle {
  apply(page: PrintPage): void;
}

// The one global <style> carrying a printable page's @page rule. @page can't be scoped to a
// component (it has no selector for emulated encapsulation to rewrite), so a print page adds it once
// its content is there and it is removed when the page is destroyed. Call from an injection
// context; `marker` names the data attribute that tells one page's style from another's.
//
// Paper is always white, whatever the app theme: the style forces a light colour scheme, and the
// dark class on <html> is taken off for the print itself (beforeprint/afterprint), so a page's
// dark: utilities never reach paper.
export function injectPrintPageStyle(marker: string): PrintPageStyle {
  const document = inject(DOCUMENT);
  const root = document.documentElement;
  const view = document.defaultView;
  let style: HTMLStyleElement | null = null;
  let wasDark = false;

  const beforePrint = () => {
    wasDark = root.classList.contains('dark');
    root.classList.remove('dark');
  };
  const afterPrint = () => {
    if (wasDark) {
      root.classList.add('dark');
    }

    wasDark = false;
  };

  view?.addEventListener('beforeprint', beforePrint);
  view?.addEventListener('afterprint', afterPrint);

  inject(DestroyRef).onDestroy(() => {
    style?.remove();
    view?.removeEventListener('beforeprint', beforePrint);
    view?.removeEventListener('afterprint', afterPrint);
    afterPrint();
  });

  return {
    apply({ size, orientation, margin }: PrintPage): void {
      style ??= document.head.appendChild(document.createElement('style'));
      style.setAttribute(`data-${marker}`, '');
      style.textContent =
        `@page { size: ${size} ${orientation}; margin: ${margin}; }\n` +
        '@media print { html, body { background: #fff !important; color-scheme: light; } }';
    },
  };
}

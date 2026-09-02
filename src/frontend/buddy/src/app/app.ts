import { Component, effect, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';

import { TranslationService } from './core/i18n/translation.service';
import { ThemeService } from './core/theme.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  templateUrl: './app.html'
})
export class App {
  private readonly translation = inject(TranslationService);
  private readonly theme = inject(ThemeService);

  // Keeps <html lang> in sync with the selected language so native form controls -- e.g. the
  // pickup planner's <input type="time"> -- render in that locale's 12h/24h convention instead of
  // always following the browser's own UI language.
  private readonly syncDocumentLanguage = effect(() => {
    document.documentElement.lang = this.translation.language();
  });

  // Tailwind's `dark:` variant (see the custom-variant in styles.css) is keyed off this class
  // rather than only `prefers-color-scheme`, so ThemeService.isDark can resolve "system" mode and
  // force "light"/"dark" regardless of the OS setting. index.html applies the same class before
  // Angular boots to avoid a flash of the wrong theme.
  private readonly syncDocumentTheme = effect(() => {
    document.documentElement.classList.toggle('dark', this.theme.isDark());
  });
}

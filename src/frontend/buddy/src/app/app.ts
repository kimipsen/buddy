import { Component, effect, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';

import { TranslationService } from './core/i18n/translation.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  private readonly translation = inject(TranslationService);

  // Keeps <html lang> in sync with the selected language so native form controls -- e.g. the
  // pickup planner's <input type="time"> -- render in that locale's 12h/24h convention instead of
  // always following the browser's own UI language.
  private readonly syncDocumentLanguage = effect(() => {
    document.documentElement.lang = this.translation.language();
  });
}

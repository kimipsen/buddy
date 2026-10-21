import { en } from './app/core/i18n/translations/en';
import { preloadDictionary } from './app/core/i18n/translations';

// The app loads every dictionary lazily and waits for it before it renders. Specs build components
// without that initializer, so English is preloaded here: they render (and assert on) English text
// synchronously, as they always have.
preloadDictionary('en', en);

// jsdom doesn't implement window.matchMedia, which ThemeService calls on construction. Without
// this, every spec that builds an App/ProfileMenu (anything that injects ThemeService through
// real DI) would throw "matchMedia is not a function". Individual specs that care about a
// specific OS preference stub this themselves (see theme.service.spec.ts); this just keeps the
// rest of the suite from crashing.
if (typeof window.matchMedia !== 'function') {
  window.matchMedia = (query: string): MediaQueryList =>
    ({
      matches: false,
      media: query,
      onchange: null,
      addListener: () => {},
      removeListener: () => {},
      addEventListener: () => {},
      removeEventListener: () => {},
      dispatchEvent: () => false,
    }) as MediaQueryList;
}

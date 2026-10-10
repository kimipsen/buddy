// Applies the saved/system theme before Angular boots so the page never flashes the wrong theme.
// Loaded as a blocking <script src> from index.html -- a file rather than an inline script, so the
// enforced Content-Security-Policy (script-src 'self', see Caddyfile) allows it. Keep the storage
// key in sync with THEME_STORAGE_KEY in src/app/core/theme-storage.ts.
(function () {
  try {
    var mode = localStorage.getItem('buddy_theme_mode');
    var prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
    if (mode === 'dark' || (mode !== 'light' && prefersDark)) {
      document.documentElement.classList.add('dark');
    }
  } catch (e) {
    // localStorage/matchMedia unavailable -- fall back to the default light theme.
  }
})();

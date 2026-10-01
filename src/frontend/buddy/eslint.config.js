// @ts-check
const eslint = require('@eslint/js');
const { defineConfig } = require('eslint/config');
const tseslint = require('typescript-eslint');
const angular = require('angular-eslint');
const prettier = require('eslint-config-prettier/flat');

module.exports = defineConfig([
  {
    ignores: ['dist/**', '.angular/**', 'coverage/**', 'reports/**', '.stryker-tmp/**', 'playwright-report/**', 'test-results/**'],
  },
  {
    files: ['**/*.ts'],
    extends: [
      eslint.configs.recommended,
      tseslint.configs.recommended,
      tseslint.configs.stylistic,
      angular.configs.tsRecommended,
    ],
    processor: angular.processInlineTemplates,
    rules: {
      '@angular-eslint/directive-selector': [
        'error',
        {
          type: 'attribute',
          prefix: 'app',
          style: 'camelCase',
        },
      ],
      '@angular-eslint/component-selector': [
        'error',
        {
          type: 'element',
          prefix: 'app',
          style: 'kebab-case',
        },
      ],
    },
  },
  {
    // Specs and the test setup use intentional no-op stubs (`async () => {}` for
    // Promise<void> service methods, matchMedia listener stubs), so empty functions
    // are expected there. Production code still gets the rule.
    files: ['src/**/*.spec.ts', 'src/test-setup.ts'],
    rules: {
      '@typescript-eslint/no-empty-function': 'off',
    },
  },
  {
    // Playwright requires fixtures to destructure their first argument
    // (`async ({}, use) => ...`); it parses the parameter list to resolve
    // fixture dependencies, so the empty pattern cannot be removed.
    files: ['e2e/**/*.ts'],
    rules: {
      'no-empty-pattern': 'off',
    },
  },
  {
    files: ['**/*.html'],
    extends: [angular.configs.templateRecommended, angular.configs.templateAccessibility],
    rules: {
      // Warn, not error: the existing hits are full-screen click-to-dismiss backdrops
      // (menus, pickers, confirm dialog) that also close on Escape, plus the dialog
      // panel's `(click)="$event.stopPropagation()"`. Making them
      // focusable or swapping them for buttons changes focus order and keyboard
      // behaviour, which needs a deliberate a11y pass rather than a lint fix.
      '@angular-eslint/template/interactive-supports-focus': 'warn',
    },
  },
  // Last, so it turns off any stylistic rules that would conflict with prettier.
  prettier,
]);

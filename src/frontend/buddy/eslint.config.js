// @ts-check
const eslint = require('@eslint/js');
const { defineConfig } = require('eslint/config');
const tseslint = require('typescript-eslint');
const angular = require('angular-eslint');
const prettier = require('eslint-config-prettier/flat');

module.exports = defineConfig([
  {
    ignores: [
      'dist/**',
      '.angular/**',
      'coverage/**',
      'reports/**',
      '.stryker-tmp/**',
      'playwright-report/**',
      'test-results/**',
    ],
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
    languageOptions: {
      parserOptions: {
        projectService: true,
        tsconfigRootDir: __dirname,
      },
    },
    rules: {
      // Null-safety guardrails (docs/backend/analysis/eliminate-nulls.md, Phase 0). These need
      // type information, hence projectService above.
      '@typescript-eslint/no-non-null-assertion': 'error',
      '@typescript-eslint/no-unnecessary-condition': 'error',
      '@typescript-eslint/prefer-nullish-coalescing': 'error',
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
    // Specs, spec helpers (src/testing) and the test setup use intentional no-op stubs (`async () => {}` for
    // Promise<void> service methods, matchMedia listener stubs), so empty functions
    // are expected there. Production code still gets the rule.
    files: ['src/**/*.spec.ts', 'src/test-setup.ts', 'src/testing/**/*.ts'],
    rules: {
      '@typescript-eslint/no-empty-function': 'off',
      // Specs assert on DOM queries and mock calls (`querySelector(...)!`, `mock.calls[0]!`), where
      // a miss should just fail the test; the null-safety rules target production code.
      '@typescript-eslint/no-non-null-assertion': 'off',
      '@typescript-eslint/no-unnecessary-condition': 'off',
      '@typescript-eslint/prefer-nullish-coalescing': 'off',
    },
  },
  {
    // Playwright requires fixtures to destructure their first argument
    // (`async ({}, use) => ...`); it parses the parameter list to resolve
    // fixture dependencies, so the empty pattern cannot be removed.
    files: ['e2e/**/*.ts', 'screenshots/**/*.ts'],
    rules: {
      'no-empty-pattern': 'off',
      '@typescript-eslint/no-non-null-assertion': 'off',
      '@typescript-eslint/no-unnecessary-condition': 'off',
      '@typescript-eslint/prefer-nullish-coalescing': 'off',
    },
  },
  {
    files: ['**/*.html'],
    extends: [angular.configs.templateRecommended, angular.configs.templateAccessibility],
  },
  // Last, so it turns off any stylistic rules that would conflict with prettier.
  prettier,
]);

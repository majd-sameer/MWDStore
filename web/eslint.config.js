// @ts-check
//
// Flat ESLint config for the MyStore Angular CLI workspace.
//
// Beyond the standard angular-eslint recommendations, this enforces module
// boundaries between the workspace's projects using
// `@typescript-eslint/no-restricted-imports`. The rules key off the import
// specifier string, which is how every cross-project import actually appears:
//   - libraries are imported by their path-alias (core, data-access, ui, util
//     -> see tsconfig `paths`);
//   - apps have no alias, so any app-to-app or lib-to-app import can only be a
//     relative path that traverses into projects/<app>/..., caught by the
//     per-project name globs.
//
// Dependency direction (apps -> libs, never the reverse):
//
//     storefront   admin          (applications — may not import each other)
//          \        /
//           v      v
//        core     ui              (feature libraries)
//           \      |
//            v     v
//         data-access  util       (base layer; data-access stays framework-pure)
//
// If we later want tag-based enforcement (declare each project's type/scope as
// tags and assert allowed edges), that's what Nx's
// `@nx/enforce-module-boundaries` provides — see the note in the task. This
// file keeps us on plain Angular CLI.
const tseslint = require('typescript-eslint');
const angular = require('angular-eslint');

// NOTE ON MATCHING: `no-restricted-imports` `group` globs use gitignore
// semantics (the `ignore` package). So an unanchored name like `core` matches a
// `core` segment ANYWHERE — including `@angular/core` — and `**/admin/**`
// matches data-access's own internal `lib/admin/` folder. To target a bare
// path-alias precisely we anchor with a leading `/` (`/core` matches the
// specifier `core`, but not `@angular/core`). A leading `!` re-allows a
// previously-matched specifier (used to keep `@angular/common/http`).

/**
 * Globs matching an application by name, for the apps-and-libs boundary. Used
 * only where the linted project has no internal folder of the same name
 * (storefront, admin, core, ui, util), so the broad form is safe and also
 * catches relative sibling-traversal imports.
 */
const projectPatterns = (name) => [
  name,
  `${name}/*`,
  `${name}/**`,
  `**/${name}`,
  `**/${name}/*`,
  `**/${name}/**`,
  `**/projects/${name}/**`,
];

const APPS = ['storefront', 'admin'];
const appPatterns = APPS.flatMap(projectPatterns);

const NO_APP_IMPORTS_MESSAGE =
  'Libraries must not import application code (storefront/admin). ' +
  'Dependencies point from apps to libs, never the reverse — move shared code into a library.';

// data-access has its OWN `lib/admin/` folder, so a broad admin glob would
// false-positive on its internal imports. These anchored / project-scoped
// patterns block importing an application (by alias, or via a projects/<app>/
// path) without snagging `lib/admin/...`.
const DATA_ACCESS_APP_PATTERNS = [
  '/storefront',
  '/storefront/**',
  '/admin',
  '/admin/**',
  '**/projects/storefront/**',
  '**/projects/admin/**',
];

/** data-access is the base layer: anchored so it never matches `@angular/core`. */
const DATA_ACCESS_LIB_PATTERNS = ['/core', '/core/**', '/ui', '/ui/**'];

// Beyond @angular/core and @angular/common/http (which data-access genuinely
// uses), every other framework entry point is off-limits so the library stays
// framework-pure: no router, forms, platform, animations, SSR, or UI kit. Each
// glob blocks the whole package subtree.
//
// `@angular/common` is handled separately as an exact `paths` entry, NOT here:
// gitignore semantics can't re-include `@angular/common/http` once its parent
// `@angular/common` directory is excluded by a glob, so blocking the bare
// specifier by exact name is the only way to keep the HTTP subpath allowed.
const FRAMEWORK_IMPURE_PATTERNS = [
  '@angular/router',
  '@angular/forms',
  '@angular/platform-browser',
  '@angular/platform-browser-dynamic',
  '@angular/platform-server',
  '@angular/animations',
  '@angular/ssr',
  '@ng-bootstrap/ng-bootstrap',
];

const FRAMEWORK_IMPURE_MESSAGE =
  'data-access stays framework-pure: only @angular/core and @angular/common/http ' +
  'are permitted (no router, forms, platform, animations, SSR, or @ng-bootstrap).';

module.exports = tseslint.config(
  {
    ignores: [
      'dist/**',
      'node_modules/**',
      'coverage/**',
      '.angular/**',
      '**/*.d.ts',
    ],
  },

  // ----- Base: TypeScript + Angular recommendations (all source) -----
  {
    files: ['**/*.ts'],
    extends: [
      ...tseslint.configs.recommended,
      ...angular.configs.tsRecommended,
    ],
    processor: angular.processInlineTemplates,
    rules: {},
  },
  {
    files: ['**/*.html'],
    extends: [
      ...angular.configs.templateRecommended,
      ...angular.configs.templateAccessibility,
    ],
    rules: {},
  },

  // ----- Boundary: storefront may not import admin -----
  {
    files: ['projects/storefront/**/*.ts'],
    rules: {
      '@typescript-eslint/no-restricted-imports': [
        'error',
        {
          patterns: [
            {
              group: projectPatterns('admin'),
              message:
                'storefront must not import from the admin app. The two apps cannot depend on each other — extract shared code into a library (core/ui/util/data-access).',
            },
          ],
        },
      ],
    },
  },

  // ----- Boundary: admin may not import storefront -----
  {
    files: ['projects/admin/**/*.ts'],
    rules: {
      '@typescript-eslint/no-restricted-imports': [
        'error',
        {
          patterns: [
            {
              group: projectPatterns('storefront'),
              message:
                'admin must not import from the storefront app. The two apps cannot depend on each other — extract shared code into a library (core/ui/util/data-access).',
            },
          ],
        },
      ],
    },
  },

  // ----- Boundary: feature libraries may not import applications -----
  {
    files: [
      'projects/core/**/*.ts',
      'projects/ui/**/*.ts',
      'projects/util/**/*.ts',
    ],
    rules: {
      '@typescript-eslint/no-restricted-imports': [
        'error',
        {
          patterns: [{ group: appPatterns, message: NO_APP_IMPORTS_MESSAGE }],
        },
      ],
    },
  },

  // ----- Boundary: data-access — base layer, framework-pure -----
  {
    files: ['projects/data-access/**/*.ts'],
    rules: {
      '@typescript-eslint/no-restricted-imports': [
        'error',
        {
          paths: [
            {
              name: '@angular/common',
              message:
                'data-access stays framework-pure: import @angular/common/http for the HTTP client; the rest of @angular/common (DOM directives/pipes) is not allowed.',
            },
          ],
          patterns: [
            { group: DATA_ACCESS_APP_PATTERNS, message: NO_APP_IMPORTS_MESSAGE },
            {
              group: DATA_ACCESS_LIB_PATTERNS,
              message:
                'data-access is the base layer and must not import core or ui. Keep dependencies pointing the other way.',
            },
            { group: FRAMEWORK_IMPURE_PATTERNS, message: FRAMEWORK_IMPURE_MESSAGE },
          ],
        },
      ],
    },
  },
);

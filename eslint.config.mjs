import js from '@eslint/js';

export default [
  {
    ignores: ['**/node_modules/**', '**/bin/**', '**/obj/**'],
  },
  js.configs.recommended,
  {
    files: ['src/InventorySync.Api/wwwroot/js/**/*.js'],
    languageOptions: {
      ecmaVersion: 2022,
      sourceType: 'module',
      globals: {
        window: 'readonly',
        document: 'readonly',
        location: 'readonly',
        fetch: 'readonly',
        AbortController: 'readonly',
        AbortSignal: 'readonly',
        setTimeout: 'readonly',
        clearTimeout: 'readonly',
        requestAnimationFrame: 'readonly',
        DOMException: 'readonly',
        URLSearchParams: 'readonly',
        console: 'readonly',
        Node: 'readonly',
      },
    },
  },
];

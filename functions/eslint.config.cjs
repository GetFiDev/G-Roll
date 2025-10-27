// functions/eslint.config.cjs
const tsPlugin = require('@typescript-eslint/eslint-plugin');
const tsParser = require('@typescript-eslint/parser');

module.exports = [
  // neleri yok sayalım
  { ignores: ['dist/**', 'lib/**', 'node_modules/**'] },

  // TS/JS dosyaları için kurallar
  {
    files: ['**/*.ts', '**/*.tsx', '**/*.js'],
    languageOptions: {
      parser: tsParser,
      ecmaVersion: 'latest',
      sourceType: 'module',
      parserOptions: {
        // type-aware kurallar istemiyorsan project vermene gerek yok
      },
    },
    plugins: {
      '@typescript-eslint': tsPlugin,
    },
    rules: {
      'object-curly-spacing': ['error', 'never'],
      'max-len': ['error', { code: 120, ignoreStrings: true, ignoreTemplateLiterals: true }],
      '@typescript-eslint/no-non-null-assertion': 'warn',
    },
  },
];

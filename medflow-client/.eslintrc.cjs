/* eslint-env node */
module.exports = {
  root: true,
  env: { browser: true, es2020: true },
  parser: '@typescript-eslint/parser',
  parserOptions: { ecmaVersion: 2020, sourceType: 'module' },
  plugins: ['@typescript-eslint', 'react-hooks'],
  extends: [
    'eslint:recommended',
    'plugin:@typescript-eslint/recommended',
    'plugin:react-hooks/recommended',
  ],
  rules: {
    // Omitting a field via rest destructuring (e.g. dropping confirmPassword before submit) is intentional.
    '@typescript-eslint/no-unused-vars': ['error', { ignoreRestSiblings: true }],
  },
  ignorePatterns: ['dist', 'node_modules', '.eslintrc.cjs'],
};

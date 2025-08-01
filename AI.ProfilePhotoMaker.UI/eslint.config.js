// @ts-check
const eslint = require("@eslint/js");
const tseslint = require("typescript-eslint");
const angular = require("angular-eslint");
const prettier = require("eslint-config-prettier");

module.exports = tseslint.config(
  {
    files: ["**/*.ts"],
    extends: [
      eslint.configs.recommended,
      ...tseslint.configs.recommended,
      ...tseslint.configs.stylistic,
      ...angular.configs.tsRecommended,
      prettier,
    ],
    processor: angular.processInlineTemplates,
    languageOptions: {
      parserOptions: {
        project: ["./tsconfig.json"],
      },
    },
    rules: {
      // Angular-specific rules
      "@angular-eslint/directive-selector": [
        "error",
        {
          type: "attribute",
          prefix: "app",
          style: "camelCase",
        },
      ],
      "@angular-eslint/component-selector": [
        "error",
        {
          type: "element",
          prefix: "app",
          style: "kebab-case",
        },
      ],
      
      // Code quality rules
      "@angular-eslint/no-empty-lifecycle-method": "error",
      "@angular-eslint/use-lifecycle-interface": "error",
      "@angular-eslint/no-conflicting-lifecycle": "error",
      "@angular-eslint/no-input-rename": "error",
      "@angular-eslint/no-output-rename": "error",
      "@angular-eslint/no-output-native": "error",
      "@angular-eslint/no-output-on-prefix": "error",
      "@angular-eslint/prefer-on-push-component-change-detection": "warn",
      
      // TypeScript rules for better code quality
      "@typescript-eslint/no-unused-vars": ["error", { "argsIgnorePattern": "^_" }],
      "@typescript-eslint/explicit-function-return-type": "warn",
      "@typescript-eslint/no-explicit-any": "warn",
      "@typescript-eslint/prefer-optional-chain": "error",
      
      // General code quality
      "max-len": ["error", { "code": 120, "ignoreUrls": true, "ignoreStrings": true }],
      "max-lines-per-function": ["warn", { "max": 50, "skipBlankLines": true, "skipComments": true }],
      "max-lines": ["warn", { "max": 400, "skipBlankLines": true, "skipComments": true }],
      "complexity": ["warn", { "max": 10 }],
      "max-depth": ["error", { "max": 4 }],
      "max-params": ["warn", { "max": 5 }],
      
      // Naming conventions
      "@typescript-eslint/naming-convention": [
        "error",
        {
          "selector": "default",
          "format": ["camelCase"]
        },
        {
          "selector": "variable",
          "format": ["camelCase", "UPPER_CASE"]
        },
        {
          "selector": "parameter",
          "format": ["camelCase"],
          "leadingUnderscore": "allow"
        },
        {
          "selector": "memberLike",
          "modifiers": ["private"],
          "format": ["camelCase"],
          "leadingUnderscore": "require"
        },
        {
          "selector": "typeLike",
          "format": ["PascalCase"]
        },
        {
          "selector": "enumMember",
          "format": ["UPPER_CASE"]
        },
        {
          "selector": "objectLiteralProperty",
          "filter": {
            "regex": "^(Authorization|X-[A-Z][a-zA-Z-]*|ngrok-skip-browser-warning|Content-Type|Accept|User-Agent|image\\/[a-z]+)$",
            "match": true
          },
          "format": null
        },
        {
          "selector": "objectLiteralProperty",
          "filter": {
            "regex": "^[a-zA-Z0-9-]+(-[a-zA-Z0-9-]*)*$|^[a-zA-Z0-9 ]+( [a-zA-Z0-9 ]*)*$",
            "match": true
          },
          "format": null
        }
      ],
      
      // Import/export rules
      "sort-imports": ["error", {
        "ignoreCase": true,
        "ignoreDeclarationSort": true,
        "ignoreMemberSort": false,
        "memberSyntaxSortOrder": ["none", "all", "multiple", "single"]
      }],
      
      // Best practices
      "no-console": ["warn", { "allow": ["warn", "error"] }],
      "no-debugger": "error",
      "no-var": "error",
      "prefer-const": "error",
      "prefer-arrow-callback": "error",
      "object-shorthand": "error",
      "no-duplicate-imports": "error",
      "eqeqeq": ["error", "always"],
      "curly": ["error", "all"]
    },
  },
  {
    files: ["**/*.html"],
    extends: [
      ...angular.configs.templateRecommended,
      ...angular.configs.templateAccessibility,
      prettier,
    ],
    rules: {
      // Template best practices
      "@angular-eslint/template/no-negated-async": "error",
      "@angular-eslint/template/use-track-by-function": "warn",
      "@angular-eslint/template/conditional-complexity": ["warn", { "maxComplexity": 3 }],
      "@angular-eslint/template/cyclomatic-complexity": ["warn", { "maxComplexity": 10 }],
      "@angular-eslint/template/no-duplicate-attributes": "error",
      "@angular-eslint/template/no-interpolation-in-attributes": "error",
      "@angular-eslint/template/no-any": "warn",
      "@angular-eslint/template/no-autofocus": "warn",
      "@angular-eslint/template/no-distracting-elements": "error",
      "@angular-eslint/template/no-positive-tabindex": "error"
    },
  },
  {
    files: ["**/*.spec.ts"],
    rules: {
      // Relaxed rules for test files
      "@typescript-eslint/no-explicit-any": "off",
      "max-lines-per-function": "off",
      "max-lines": "off",
      "@typescript-eslint/explicit-function-return-type": "off"
    }
  }
);

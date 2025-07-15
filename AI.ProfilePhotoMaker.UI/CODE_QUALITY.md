# Code Quality Standards

This document outlines the code quality standards and automated enforcement setup for the AI.ProfilePhotoMaker UI project.

## Overview

The project uses automated code quality tools to maintain consistent code style, enforce best practices, and catch potential issues early in the development process.

## Tools Used

### ESLint
- **Purpose**: JavaScript/TypeScript linting and code analysis
- **Configuration**: `eslint.config.js`
- **Extends**: Angular ESLint rules, TypeScript ESLint rules, Prettier integration

### Prettier
- **Purpose**: Code formatting and style consistency
- **Configuration**: `.prettierrc`
- **Supported Files**: TypeScript, HTML, SCSS, JSON, Markdown

### Husky
- **Purpose**: Git hooks management
- **Configuration**: `.husky/pre-commit`
- **Hooks**: Pre-commit quality checks

### lint-staged
- **Purpose**: Run linting/formatting only on staged files
- **Configuration**: `package.json` lint-staged section

## Quality Rules

### Angular-Specific Rules

#### Component Guidelines
- **Selector Style**: Use kebab-case for component selectors with 'app' prefix
- **Directive Style**: Use camelCase for directive selectors with 'app' prefix
- **Change Detection**: Components should use OnPush change detection (warning)
- **Lifecycle**: Implement lifecycle interfaces, avoid empty lifecycle methods

#### Template Guidelines
- **Accessibility**: Follow accessibility best practices
- **Complexity**: Keep template complexity low (max 3 conditional complexity)
- **Performance**: Use TrackBy functions for *ngFor loops
- **Best Practices**: Avoid negated async pipes, no duplicate attributes

### TypeScript Rules

#### Code Quality
- **Line Length**: Maximum 120 characters
- **Function Length**: Maximum 50 lines per function
- **File Length**: Maximum 400 lines per file
- **Complexity**: Maximum cyclomatic complexity of 10
- **Depth**: Maximum nesting depth of 4 levels
- **Parameters**: Maximum 5 parameters per function

#### Type Safety
- **Explicit Types**: Return types required on functions (warning)
- **No Any**: Avoid using 'any' type (warning)
- **Unused Variables**: Parameters starting with '_' are allowed as unused
- **Optional Chaining**: Use optional chaining where possible

#### Naming Conventions
- **Variables**: camelCase or UPPER_CASE (constants)
- **Functions**: camelCase
- **Classes**: PascalCase
- **Interfaces**: PascalCase
- **Enums**: PascalCase with UPPER_CASE members
- **Private Members**: Must start with underscore (_)

### Code Standards

#### Import Organization
- **Sorting**: Imports should be sorted alphabetically
- **Grouping**: External libraries first, then internal modules
- **No Duplicates**: Avoid duplicate imports

#### Best Practices
- **Console Usage**: Only console.warn and console.error allowed
- **Debugging**: No debugger statements in production code
- **Modern JavaScript**: Use const/let instead of var, prefer arrow functions
- **Equality**: Use strict equality (===) always
- **Curly Braces**: Required for all control structures

### Test File Exceptions

Test files (`*.spec.ts`) have relaxed rules:
- Explicit return types not required
- Line and function length limits disabled
- Any type allowed for testing scenarios

## Usage

### Available Scripts

#### Quality Check Scripts
```bash
# Run linting only
npm run lint

# Run linting with auto-fix
npm run lint:fix

# Check code formatting
npm run format:check

# Fix code formatting
npm run format

# Run complete quality check (lint + format check)
npm run quality:check

# Fix all quality issues (lint + format)
npm run quality:fix
```

#### Development Workflow
```bash
# Before committing, run quality checks
npm run quality:check

# Fix issues automatically
npm run quality:fix

# Individual file linting
npx eslint src/app/components/my-component.ts

# Individual file formatting
npx prettier --write src/app/components/my-component.ts
```

### Pre-commit Hooks

The project automatically runs quality checks on staged files before each commit:

1. **Staged Files Only**: Only files staged for commit are processed
2. **Auto-fix**: ESLint and Prettier automatically fix issues where possible
3. **Commit Block**: Commit is blocked if unfixable issues remain

### File Coverage

#### Processed by Quality Tools
- **TypeScript**: `*.ts`, `*.js` - ESLint + Prettier
- **HTML**: `*.html` - Prettier only
- **Styles**: `*.scss`, `*.css` - Prettier only
- **Config**: `*.json` - Prettier only
- **Docs**: `*.md` - Prettier only

#### Excluded Files
- **SASS**: `*.sass` files (indented syntax not well-supported by Prettier)
- **Generated**: `node_modules/`, `dist/`, `.angular/`
- **Assets**: `src/assets/models/` (ML model files)

## Configuration Files

### ESLint Configuration (`eslint.config.js`)
```javascript
// Key sections:
// - TypeScript + Angular rules
// - Custom naming conventions
// - Template accessibility rules
// - Test file exceptions
```

### Prettier Configuration (`.prettierrc`)
```json
{
  "semi": true,
  "trailingComma": "es5",
  "singleQuote": true,
  "printWidth": 100,
  "tabWidth": 2,
  "useTabs": false
}
```

### Husky Pre-commit Hook (`.husky/pre-commit`)
```bash
cd AI.ProfilePhotoMaker.UI && npx lint-staged
```

### lint-staged Configuration (`package.json`)
```json
{
  "lint-staged": {
    "src/**/*.{ts,js}": ["eslint --fix", "prettier --write"],
    "src/**/*.{html}": ["prettier --write"],
    "src/**/*.{scss,css}": ["prettier --write"],
    "src/**/*.{json,md}": ["prettier --write"]
  }
}
```

## IDE Integration

### VS Code
Recommended extensions:
- ESLint (Microsoft)
- Prettier (Prettier)
- Angular Language Service (Angular)

### Settings
Add to VS Code settings.json:
```json
{
  "editor.formatOnSave": true,
  "editor.defaultFormatter": "esbenp.prettier-vscode",
  "eslint.validate": ["typescript", "html"],
  "editor.codeActionsOnSave": {
    "source.fixAll.eslint": true
  }
}
```

## Troubleshooting

### Common Issues

#### ESLint Errors
- **Type Information**: Ensure tsconfig.json is properly configured
- **Module Resolution**: Check import paths and module declarations
- **Rule Conflicts**: Prettier rules may conflict with ESLint formatting rules

#### Prettier Formatting
- **SASS Files**: Currently excluded due to parser limitations
- **Template Files**: Angular template parsing requires specific configuration
- **Line Endings**: Configured for LF (Unix) line endings

#### Pre-commit Hook Issues
- **Hook Not Running**: Ensure Husky is installed and initialized
- **Staged Files**: Only staged files are processed by lint-staged
- **Root Directory**: Husky must be run from project root directory

### Debugging Commands
```bash
# Check ESLint configuration
npx eslint --print-config src/app/app.component.ts

# Test Prettier on specific file
npx prettier --check src/app/app.component.ts

# Debug lint-staged
npx lint-staged --debug

# Check Husky installation
npx husky
```

## Best Practices

### For Developers

1. **Run Quality Checks**: Always run `npm run quality:check` before committing
2. **Fix Issues Early**: Address linting warnings as you develop
3. **Use IDE Integration**: Configure your IDE to show ESLint/Prettier issues
4. **Understand Rules**: Read error messages and understand the reasoning
5. **Consistent Style**: Let Prettier handle formatting, focus on logic

### For Code Reviews

1. **Automated Quality**: Quality issues should be caught by automation
2. **Focus on Logic**: Review business logic and architecture
3. **Test Coverage**: Ensure quality rules don't hinder test coverage
4. **Performance**: Consider performance implications of quality rules

### For New Components

1. **Start with Quality**: Run linting as you develop
2. **Component Size**: Keep components under 400 lines
3. **Function Size**: Keep functions under 50 lines
4. **Type Safety**: Add explicit return types for public methods
5. **Accessibility**: Follow template accessibility guidelines

## Continuous Improvement

This quality setup should be regularly reviewed and updated to:
- Align with latest Angular best practices
- Incorporate new ESLint rules
- Improve developer experience
- Maintain code consistency across the team

For questions or suggestions about code quality standards, please refer to the project documentation or create an issue in the project repository.
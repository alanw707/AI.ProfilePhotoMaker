# Pre-Commit Linting Guide

This project uses automated pre-commit and pre-push hooks to ensure code quality and prevent linting errors from reaching the CI/CD pipeline.

## 🔧 What's Configured

### Pre-Commit Hook
- **Runs**: `lint-staged` for changed files only
- **Actions**: 
  - Auto-formats code with Prettier
  - Auto-fixes ESLint issues where possible
  - Validates TypeScript/JavaScript and HTML template files
- **Speed**: Typically < 30 seconds for normal commits

### Pre-Push Hook
- **Runs**: Full project lint check (errors only)
- **Actions**:
  - Validates entire codebase for errors
  - Runs TypeScript compilation check
  - Blocks push if critical errors exist
- **Purpose**: Catch any issues missed by pre-commit

## 🚀 Available Commands

### Quick Fixes
```bash
# Fix linting issues in changed files only (fastest)
npm run lint:changed

# Auto-fix all linting issues in entire project
npm run lint:fix

# Fix all formatting issues
npm run format

# Fix both linting and formatting (recommended)
npm run quality:fix
```

### Validation Commands
```bash
# Check linting (shows all issues)
npm run lint

# Check linting (errors only, quieter)
npm run lint:errors-only

# Check formatting
npm run format:check

# Check both linting and formatting
npm run quality:check
```

### Manual Hook Testing
```bash
# Test pre-commit hook manually
npm run precommit

# Test pre-push validation manually
npm run prepush
```

## 🛠️ Troubleshooting

### Common Issues & Solutions

#### "Pre-commit hook failed with linting errors"
```bash
# Quick fix: Auto-fix common issues
npm run quality:fix

# Add fixed files and commit again
git add .
git commit
```

#### "Too many linting warnings/errors"
The pre-commit hook focuses on **changed files only** and allows warnings. Only errors block commits.

```bash
# See what specific issues exist
npm run lint

# Fix specific files
npx eslint src/path/to/file.ts --fix
```

#### "Pre-push hook failing"
The pre-push hook is stricter and checks the entire project for errors.

```bash
# Check what errors exist
npm run lint:errors-only

# Fix critical errors only
npm run lint:fix
```

#### "Hook not running"
```bash
# Reinstall git hooks
npm run prepare

# Check hook permissions
ls -la .husky/
chmod +x .husky/pre-commit .husky/pre-push
```

### Emergency Bypass (Use Sparingly)
```bash
# Skip pre-commit hook (NOT recommended)
git commit --no-verify

# Skip pre-push hook (NOT recommended)
git push --no-verify
```

## 📋 Linting Rules Summary

### Error Level (Blocks commits/pushes)
- TypeScript compilation errors
- Angular template syntax errors
- Accessibility violations (missing key events, focus support)
- Naming convention violations (private members must have underscore)
- Duplicate imports
- Line length > 120 characters
- Unused variables (unless prefixed with _)

### Warning Level (Allows commits)
- Missing return types
- Complexity violations
- Missing trackBy functions
- `any` type usage
- Console.log statements

## 🎯 Best Practices

1. **Commit frequently** with small, focused changes
2. **Run `npm run quality:fix`** before large commits
3. **Use `npm run lint:changed`** to check your changes before committing
4. **Prefix unused parameters** with underscore: `_unused`
5. **Add return types** to functions for better code clarity
6. **Use trackBy functions** in ngFor loops for performance

## 🔍 IDE Integration

### VS Code
Install these extensions for real-time linting:
- ESLint
- Prettier - Code formatter
- Angular Language Service

Add to your VS Code settings.json:
```json
{
  "editor.formatOnSave": true,
  "editor.codeActionsOnSave": {
    "source.fixAll.eslint": true
  },
  "eslint.validate": ["typescript", "javascript", "html"]
}
```

### WebStorm/IntelliJ
1. Enable ESLint: Settings → Languages & Frameworks → JavaScript → Code Quality Tools → ESLint
2. Enable Prettier: Settings → Languages & Frameworks → JavaScript → Prettier
3. Set "Run eslint --fix" on save

## 🎉 Success Indicators

When everything is working properly:
- ✅ Commits complete quickly (< 30 seconds)
- ✅ No linting errors reach CI/CD pipeline
- ✅ Code is consistently formatted
- ✅ TypeScript compilation is always clean
- ✅ Team maintains high code quality standards

## 🆘 Getting Help

If you're still having issues:
1. Check this guide first
2. Run `npm run quality:fix` to auto-resolve common issues
3. Ask a team member familiar with the linting setup
4. Check the project's ESLint configuration in `eslint.config.js`
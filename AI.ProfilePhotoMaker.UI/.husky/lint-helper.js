#!/usr/bin/env node

/**
 * Lint Helper - Pre-commit linting utility
 * Provides intelligent linting based on changed files and current state
 */

const { execSync } = require('child_process');
const fs = require('fs');
const path = require('path');

function runCommand(command, description) {
  console.log(`🔄 ${description}...`);
  try {
    const output = execSync(command, { 
      encoding: 'utf8', 
      stdio: ['pipe', 'pipe', 'pipe'],
      cwd: __dirname 
    });
    console.log(`✅ ${description} completed`);
    return { success: true, output };
  } catch (error) {
    console.error(`❌ ${description} failed:`);
    console.error(error.stdout || error.message);
    return { success: false, error: error.stdout || error.message };
  }
}

function getStagedFiles() {
  try {
    const output = execSync('git diff --cached --name-only', { encoding: 'utf8' });
    return output.trim().split('\n').filter(file => file.length > 0);
  } catch (error) {
    return [];
  }
}

function main() {
  console.log('🚀 Pre-commit Lint Helper Starting...\n');
  
  const stagedFiles = getStagedFiles();
  const hasTypeScriptFiles = stagedFiles.some(file => file.endsWith('.ts') || file.endsWith('.js'));
  const hasTemplateFiles = stagedFiles.some(file => file.endsWith('.html'));
  
  if (!hasTypeScriptFiles && !hasTemplateFiles) {
    console.log('📝 No TypeScript or template files staged, skipping linting');
    return;
  }
  
  console.log(`📋 Found ${stagedFiles.length} staged files`);
  console.log(`   - TypeScript/JS files: ${hasTypeScriptFiles ? 'Yes' : 'No'}`);
  console.log(`   - Template files: ${hasTemplateFiles ? 'Yes' : 'No'}\n`);
  
  // Run lint-staged which will handle formatting and linting
  const lintResult = runCommand('npx lint-staged', 'Running lint-staged');
  
  if (!lintResult.success) {
    console.log('\n❌ Pre-commit validation failed!');
    console.log('\n🔧 Quick fixes you can try:');
    console.log('   npm run lint:fix     - Auto-fix linting issues');
    console.log('   npm run format       - Fix formatting issues');
    console.log('   npm run quality:fix  - Fix both linting and formatting');
    console.log('\n📖 For more help, check the pre-commit guide in docs/');
    process.exit(1);
  }
  
  console.log('\n🎉 Pre-commit validation successful!');
}

if (require.main === module) {
  main();
}
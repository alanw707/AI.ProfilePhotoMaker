const fs = require('fs');

const data = JSON.parse(fs.readFileSync('lint-output.json', 'utf8'));
const unusedVarsFiles = [];

for (const file of data) {
  const unusedVars = file.messages.filter(msg => 
    msg.ruleId === '@typescript-eslint/no-unused-vars'
  );
  
  if (unusedVars.length > 0) {
    unusedVarsFiles.push({
      filePath: file.filePath,
      errors: unusedVars.map(err => ({
        line: err.line,
        column: err.column,
        message: err.message
      }))
    });
  }
}

console.log('Files with unused variable errors:');
console.log(JSON.stringify(unusedVarsFiles, null, 2));
console.log(`\nTotal files: ${unusedVarsFiles.length}`);
console.log(`Total errors: ${unusedVarsFiles.reduce((sum, f) => sum + f.errors.length, 0)}`);
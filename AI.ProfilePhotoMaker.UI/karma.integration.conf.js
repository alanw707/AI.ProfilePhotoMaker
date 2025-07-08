// Karma configuration file for integration tests only
// Extends the base karma.conf.js but focuses on integration tests

module.exports = function (config) {
  const baseConfig = require('./karma.conf.js');
  
  baseConfig(config);
  
  // Override specific settings for integration tests
  config.set({
    files: [
      'src/app/services/services-integration.spec.ts'
    ],
    preprocessors: {
      'src/app/services/services-integration.spec.ts': ['webpack', 'sourcemap']
    },
    // Exclude all other test files
    exclude: [
      'src/**/*.spec.ts',
      '!src/app/services/services-integration.spec.ts'
    ]
  });
};
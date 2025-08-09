// Karma configuration file for integration tests only
// Extends the base karma.conf.js but focuses on integration tests

module.exports = function (config) {
  const baseConfig = require('./karma.conf.js');
  
  baseConfig(config);
  
  // Override specific settings for integration tests
  config.set({
    // Include integration test files
    files: [
      'src/test.ts'
    ],
    // Only run integration tests
    exclude: [
      'src/**/*.spec.ts',
      '!src/app/services/services-integration.spec.ts',
      '!src/app/integration-tests/**/*.integration.spec.ts'
    ],
    // Extended timeout for integration tests
    browserDisconnectTimeout: 10000,
    browserDisconnectTolerance: 1,
    browserNoActivityTimeout: 60000,
    captureTimeout: 60000,
    // Custom reporter for integration tests
    reporters: ['progress', 'kjhtml'],
    // Custom test patterns
    client: {
      clearContext: false, // leave Jasmine Spec Runner output visible in browser
      jasmine: {
        // Extended timeout for integration tests
        DEFAULT_TIMEOUT_INTERVAL: 30000
      }
    }
  });
};
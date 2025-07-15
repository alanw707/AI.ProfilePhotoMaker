// Karma configuration file for integration tests only
// Extends the base karma.conf.js but focuses on integration tests

module.exports = function (config) {
  const baseConfig = require('./karma.conf.js');
  
  baseConfig(config);
  
  // Override specific settings for integration tests
  config.set({
    files: [
      // Original service integration tests
      'src/app/services/services-integration.spec.ts',
      // New comprehensive integration tests
      'src/app/integration-tests/integration-test-runner.spec.ts',
      'src/app/integration-tests/auth-flow.integration.spec.ts',
      'src/app/integration-tests/photo-enhancement-flow.integration.spec.ts',
      'src/app/integration-tests/photo-generation-flow.integration.spec.ts',
      'src/app/integration-tests/gallery-management-flow.integration.spec.ts'
    ],
    preprocessors: {
      'src/app/services/services-integration.spec.ts': ['webpack', 'sourcemap'],
      'src/app/integration-tests/**/*.spec.ts': ['webpack', 'sourcemap']
    },
    // Exclude all other test files
    exclude: [
      'src/**/*.spec.ts',
      '!src/app/services/services-integration.spec.ts',
      '!src/app/integration-tests/**/*.spec.ts'
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
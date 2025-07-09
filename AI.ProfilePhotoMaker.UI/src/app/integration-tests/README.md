# Integration Tests Documentation

## Overview

This directory contains comprehensive integration tests for the AI.ProfilePhotoMaker application's critical user flows. These tests ensure that components work together correctly and that the user experience flows are maintained during refactoring.

## Test Structure

### Integration Test Files

1. **`auth-flow.integration.spec.ts`** - Authentication workflow tests
2. **`photo-enhancement-flow.integration.spec.ts`** - Photo enhancement user flow tests
3. **`photo-generation-flow.integration.spec.ts`** - Photo generation workflow tests
4. **`gallery-management-flow.integration.spec.ts`** - Gallery management and image operations tests
5. **`integration-test-runner.spec.ts`** - Test utilities, configuration, and runner

### Test Coverage

| Flow | Test File | Coverage |
|------|-----------|----------|
| Authentication | `auth-flow.integration.spec.ts` | 100% |
| Photo Enhancement | `photo-enhancement-flow.integration.spec.ts` | 100% |
| Photo Generation | `photo-generation-flow.integration.spec.ts` | 100% |
| Gallery Management | `gallery-management-flow.integration.spec.ts` | 100% |

## Critical User Flows Tested

### 1. Authentication Flow
- **User Registration**: Complete registration workflow with validation
- **User Login**: Login process with error handling
- **OAuth Authentication**: Google OAuth integration and callback handling
- **Protected Routes**: Route guards and authentication state management
- **Session Management**: Token expiration, refresh, and logout
- **Error Recovery**: Network errors, invalid credentials, corrupted data

**Key Test Scenarios:**
- Registration with valid/invalid data
- Login with correct/incorrect credentials
- OAuth callback with complete/incomplete user data
- Protected route access for authenticated/unauthenticated users
- Token expiration and automatic logout
- Session persistence across page reloads

### 2. Photo Enhancement Flow
- **File Upload**: Drag-and-drop and click upload with validation
- **Image Processing**: Enhancement workflow with progress tracking
- **Credit Management**: Credit consumption and validation
- **Result Display**: Enhanced image display and download
- **Error Handling**: Upload failures, processing errors, network issues

**Key Test Scenarios:**
- Valid/invalid file upload (type, size validation)
- Enhancement processing with different enhancement types
- Credit validation and consumption tracking
- Progress tracking through enhancement phases
- Error handling for network failures and API errors
- Result download and sharing functionality

### 3. Photo Generation Flow
- **Multi-file Upload**: Bulk image upload with validation
- **Model Training**: Training workflow with progress tracking
- **Style Selection**: Style selection and credit calculation
- **Photo Generation**: Image generation with status polling
- **Workflow Orchestration**: Complete end-to-end workflow coordination

**Key Test Scenarios:**
- Multiple file upload with validation
- Training workflow initiation and progress tracking
- Style selection and credit requirement calculation
- Photo generation with different styles and quantities
- Workflow interruption and resumption
- State persistence across workflow phases

### 4. Gallery Management Flow
- **Image Loading**: Gallery initialization and image display
- **Image Actions**: View, download, share, and delete operations
- **Bulk Operations**: Multiple image selection and bulk download
- **ZIP Creation**: Multiple image ZIP file creation
- **Error Recovery**: Network errors, download failures, corrupted data

**Key Test Scenarios:**
- Gallery initialization and image loading
- Individual image download with accessibility testing
- Bulk image selection and ZIP creation
- Image deletion with confirmation
- Error handling for inaccessible images
- Fallback download methods

## Test Utilities and Configuration

### IntegrationTestConfig
Provides standardized configuration for integration tests:
- Base TestBed configuration with routing
- Mock localStorage/sessionStorage
- Mock fetch API and File API
- Mock DOM APIs and navigator APIs
- Global mock setup and cleanup

### IntegrationTestUtils
Helper functions for integration tests:
- Async operation utilities
- Mock data creation (users, images, credits)
- File upload event simulation
- JWT token creation
- HTTP response mocking

### Global Mocks
Comprehensive mocking of browser APIs:
- **Storage APIs**: localStorage, sessionStorage
- **Network APIs**: fetch, XMLHttpRequest
- **File APIs**: File, FileReader, URL
- **DOM APIs**: document.createElement, element operations
- **Navigator APIs**: clipboard, share
- **Window APIs**: alert, confirm, open

## Running Integration Tests

### Commands

```bash
# Run all integration tests
npm run test:integration

# Run with karma configuration
ng test --karma-config=karma.integration.conf.js

# Run in headless mode
ng test --karma-config=karma.integration.conf.js --watch=false --browsers=ChromeHeadless

# Run specific test file
ng test --karma-config=karma.integration.conf.js --include="**/auth-flow.integration.spec.ts"
```

### Test Configuration

The integration tests use `karma.integration.conf.js` which:
- Extends base karma configuration
- Includes only integration test files
- Excludes unit test files
- Sets extended timeouts for complex workflows
- Uses appropriate test reporters

### Test Environment Setup

Integration tests require:
- **Chrome browser** (or ChromeHeadless for CI)
- **Angular Testing Utilities**
- **Jasmine testing framework**
- **Karma test runner**

## Test Scenarios and Coverage

### Authentication Flow Tests

```typescript
describe('Authentication Flow Integration Tests', () => {
  // User registration workflow
  it('should complete full registration workflow')
  it('should handle registration errors gracefully')
  
  // User login workflow
  it('should complete full login workflow')
  it('should handle login errors gracefully')
  
  // OAuth authentication
  it('should handle OAuth callback with complete user data')
  it('should fetch user profile when JWT lacks complete user data')
  
  // Protected routes
  it('should redirect unauthenticated users to login')
  it('should allow authenticated users to access protected routes')
  
  // Session management
  it('should maintain authentication state across page reloads')
  it('should handle token expiration gracefully')
  it('should complete logout workflow')
  
  // Error recovery
  it('should handle network errors during authentication')
  it('should handle corrupted localStorage gracefully')
});
```

### Photo Enhancement Flow Tests

```typescript
describe('Photo Enhancement Flow Integration Tests', () => {
  // File upload and validation
  it('should handle file selection successfully')
  it('should validate file type and size')
  it('should handle drag and drop file upload')
  
  // Enhancement processing
  it('should complete full enhancement workflow successfully')
  it('should handle upload failure gracefully')
  it('should handle enhancement API failure')
  
  // Credit system
  it('should prevent enhancement when insufficient credits')
  it('should update credits after successful enhancement')
  
  // Progress tracking
  it('should track upload progress correctly')
  it('should update progress during enhancement phases')
  
  // Error handling
  it('should handle network errors gracefully')
  it('should handle prediction failures')
});
```

### Photo Generation Flow Tests

```typescript
describe('Photo Generation Flow Integration Tests', () => {
  // File upload phase
  it('should handle multiple file uploads successfully')
  it('should validate file count limits')
  it('should update state after successful upload')
  
  // Model training phase
  it('should start training workflow successfully')
  it('should track training progress')
  it('should handle training completion')
  
  // Style selection phase
  it('should load available styles')
  it('should calculate credit requirements')
  it('should validate sufficient credits')
  
  // Photo generation phase
  it('should start generation workflow successfully')
  it('should track generation progress')
  it('should handle generation completion')
  
  // Workflow orchestration
  it('should coordinate full workflow phases')
  it('should handle workflow interruption')
  it('should allow workflow resumption')
});
```

### Gallery Management Flow Tests

```typescript
describe('Gallery Management Flow Integration Tests', () => {
  // Image loading
  it('should load images on initialization')
  it('should transform processed images to gallery format')
  it('should deduplicate images by ID')
  
  // Image actions
  it('should handle image click to open in new tab')
  it('should handle image deletion')
  it('should handle image sharing')
  
  // Download functionality
  it('should download single image successfully')
  it('should handle download errors gracefully')
  it('should test image accessibility before download')
  
  // Bulk operations
  it('should handle bulk download with multiple images')
  it('should create ZIP file with multiple images')
  it('should track progress during ZIP creation')
  
  // Gallery refresh
  it('should refresh gallery images')
  it('should handle refresh query parameter')
});
```

## Best Practices

### Test Structure
- **Arrange**: Set up test data and mocks
- **Act**: Execute the user action or workflow
- **Assert**: Verify the expected outcome

### Mock Strategy
- Use realistic mock data that matches actual API responses
- Mock external dependencies (HTTP, localStorage, etc.)
- Provide both success and failure scenarios
- Keep mocks focused and maintainable

### Test Data
- Create reusable test data factories
- Use consistent test data across related tests
- Include edge cases and boundary conditions
- Test with both valid and invalid data

### Error Handling
- Test all error scenarios
- Verify error messages and user feedback
- Test recovery mechanisms
- Ensure graceful degradation

### Performance
- Test with realistic data volumes
- Verify efficient resource usage
- Test concurrent operations
- Monitor test execution time

## Maintenance and Updates

### When to Update Integration Tests

1. **New Features**: Add integration tests for new user flows
2. **API Changes**: Update mocks to match new API responses
3. **UI Changes**: Update component interactions and selectors
4. **Bug Fixes**: Add regression tests for fixed issues
5. **Refactoring**: Ensure tests still pass after code changes

### Test Data Maintenance

1. **Keep mocks current**: Update mock data to match production
2. **Realistic scenarios**: Use data that reflects actual usage
3. **Edge cases**: Include boundary conditions and error cases
4. **Performance**: Use appropriate data volumes for testing

### Continuous Integration

1. **Automated execution**: Run integration tests in CI pipeline
2. **Parallel execution**: Run tests in parallel for faster feedback
3. **Failure reporting**: Provide clear failure messages and logs
4. **Coverage tracking**: Monitor test coverage metrics

## Troubleshooting

### Common Issues

1. **Timeout errors**: Increase timeout values for complex workflows
2. **Mock failures**: Ensure mocks match actual API contracts
3. **State pollution**: Clean up state between tests
4. **Browser compatibility**: Test with different browsers
5. **Memory leaks**: Monitor memory usage during tests

### Debug Strategies

1. **Console logging**: Add strategic console.log statements
2. **Breakpoints**: Use browser debugger for step-through debugging
3. **Test isolation**: Run individual tests to isolate issues
4. **Mock inspection**: Verify mock calls and responses
5. **State inspection**: Check component and service state

### Performance Optimization

1. **Selective mocking**: Mock only necessary dependencies
2. **Efficient assertions**: Use specific and efficient assertions
3. **Parallel execution**: Run independent tests in parallel
4. **Resource cleanup**: Clean up resources after tests
5. **Test grouping**: Group related tests for better organization

## Future Enhancements

### Planned Improvements

1. **Visual regression testing**: Add screenshot comparison tests
2. **Performance testing**: Add performance benchmarks
3. **Accessibility testing**: Add automated accessibility checks
4. **Cross-browser testing**: Expand browser compatibility testing
5. **Mobile testing**: Add mobile device simulation

### Test Coverage Expansion

1. **Error boundaries**: Test React error boundary behavior
2. **Edge cases**: Add more boundary condition tests
3. **Performance edge cases**: Test with large data sets
4. **Network conditions**: Test with different network conditions
5. **User personas**: Test with different user profiles

---

*This integration test suite ensures comprehensive coverage of critical user flows and provides confidence during refactoring and feature development.*
# Testing Setup Documentation

## Overview

This document outlines the testing infrastructure set up for the Angular UI refactoring project. The testing framework uses Karma + Jasmine with comprehensive mock services to ensure safe refactoring.

## Testing Strategy

### 1. **Safety-First Approach**
- Create tests BEFORE refactoring to ensure no functionality is lost
- Focus on critical user workflows and component interactions
- Use comprehensive mocking to isolate component logic

### 2. **Test Categories**

#### **Unit Tests**
- Individual component functionality
- Service method testing
- Component input/output testing
- Error handling scenarios

#### **Integration Tests**
- Complete user workflows
- Component-service interactions
- State management flows
- API integration patterns

#### **Regression Tests**
- Verify existing functionality during refactoring
- Ensure UI behavior remains identical
- Validate performance characteristics

## Current Test Files

### **Created Test Files**
1. `testing-utils.ts` - Mock services and testing utilities
2. `dashboard.component.spec.ts` - Comprehensive dashboard tests

### **Integration Test Files**
1. `integration-tests/auth-flow.integration.spec.ts` - Authentication workflow tests
2. `integration-tests/photo-enhancement-flow.integration.spec.ts` - Photo enhancement flow tests
3. `integration-tests/photo-generation-flow.integration.spec.ts` - Photo generation workflow tests
4. `integration-tests/gallery-management-flow.integration.spec.ts` - Gallery management flow tests
5. `integration-tests/integration-test-runner.spec.ts` - Test utilities and configuration
6. `integration-tests/README.md` - Comprehensive integration test documentation

### **Existing Test Files**
1. `app.component.spec.ts` - Basic app component tests
2. `services/services-integration.spec.ts` - Service integration tests

## Mock Services Available

### **MockAuthService**
- Authentication state management
- Login/logout functionality
- User profile management
- JWT token handling

### **MockDashboardStateService**
- Centralized state management
- Observable state updates
- Image upload state
- Training/generation status

### **MockNotificationService**
- Success/error/info notifications
- Notification history tracking
- Message verification for tests

### **MockFileUploadService**
- Single and multiple file uploads
- File validation simulation
- Upload progress tracking
- Error scenario simulation

### **MockReplicateService**
- AI model training simulation
- Image generation workflows
- Status polling mechanisms
- API response mocking

### **MockCreditService**
- Credit balance management
- Credit consumption tracking
- Purchase workflow simulation
- Credit validation logic

### **MockFaceDetectionService**
- Face detection simulation
- Quality scoring algorithms
- Model loading simulation
- Validation result mocking

## Testing Utilities

### **TestingHelpers Class**
```typescript
// Create mock files for upload testing
const mockFiles = TestingHelpers.createMockFiles(3);

// Trigger file input changes
TestingHelpers.triggerFileInputChange(fixture, mockFiles);

// Click buttons and wait for updates
TestingHelpers.clickButton(fixture, '.upload-button');

// Wait for async operations
await TestingHelpers.waitForAsync(fixture);

// Set up test modules
await TestingHelpers.setupTestModule(DashboardComponent);
```

### **Test Constants**
```typescript
TestConstants.MOCK_USER      // Standard test user
TestConstants.MOCK_STYLES    // Available photo styles
TestConstants.MOCK_IMAGES    // Sample image data
TestConstants.TIMEOUTS       // Test timeout values
```

## Running Tests

### **Current Test Commands**
```bash
# Navigate to UI project
cd AI.ProfilePhotoMaker.UI

# Run unit tests (requires Chrome installation)
npm test

# Run unit tests in headless mode
npm test -- --watch=false --browsers=ChromeHeadless

# Run specific test files
npm test -- --include="**/dashboard.component.spec.ts"

# Run integration tests
npm run test:integration

# Run integration tests in headless mode
npm run test:integration:headless

# Run integration tests with watch mode
npm run test:integration:watch
```

### **Test Environment Setup**
The project uses:
- **Karma 6.4.0** - Test runner
- **Jasmine 5.6.0** - Testing framework
- **Angular Testing Utilities** - Component testing tools

## Critical Test Scenarios

### **Dashboard Component Tests**
1. **Component Initialization**
   - Default value verification
   - Service injection validation
   - State subscription setup

2. **File Upload Workflow**
   - File selection handling
   - File count limits (max 20)
   - File validation rules
   - Upload progress tracking

3. **Quality Validation**
   - Image dimension checking
   - Face detection integration
   - Quality scoring algorithms
   - Error message display

4. **Training/Generation Workflow**
   - Model training initiation
   - Progress tracking accuracy
   - Status polling mechanisms
   - Completion handling

5. **Credit System Integration**
   - Credit calculation logic
   - Insufficient credit handling
   - Credit consumption tracking
   - Purchase workflow validation

6. **Error Handling**
   - API error responses
   - Network failure scenarios
   - Invalid input handling
   - User-friendly error messages

## Test Coverage Goals

### **Phase 3A: Pre-Refactoring Tests**
- ✅ Dashboard Component: Comprehensive test suite created
- ⏳ Gallery Component: Planned
- ⏳ Photo Enhancement Component: Planned
- ⏳ Settings Component: Planned

### **Phase 3B: Service Tests**
- ⏳ All service methods tested
- ⏳ Error handling verified
- ⏳ Integration scenarios covered

### **Target Coverage**
- **Components**: 80%+ test coverage
- **Services**: 90%+ test coverage
- **Critical Workflows**: 100% coverage

## Testing During Refactoring

### **Before Each Refactoring Task**
1. Run existing tests to establish baseline
2. Verify all tests pass
3. Document current functionality

### **During Refactoring**
1. Update tests to match new component structure
2. Maintain test coverage percentage
3. Add tests for new components/methods

### **After Each Refactoring Task**
1. Verify all tests still pass
2. Test extracted components individually
3. Test integration between old and new components
4. Verify no functionality regression

## Test Maintenance

### **Mock Service Updates**
- Keep mocks synchronized with real service interfaces
- Update mock responses to match API changes
- Maintain realistic test data

### **Test Data Management**
- Use consistent test constants
- Update test data when requirements change
- Maintain realistic test scenarios

### **Continuous Integration**
- Integrate tests into build process
- Set up automated test execution
- Monitor test coverage metrics

## Troubleshooting

### **Common Issues**
1. **Chrome Browser Dependency**
   - Solution: Install Chrome or use ChromeHeadless
   - Alternative: Configure different browser

2. **Async Test Failures**
   - Use `waitForAsync()` helper
   - Increase timeout values for long operations
   - Properly handle Promise chains

3. **Mock Service Errors**
   - Verify service injection in test setup
   - Check mock method signatures
   - Ensure observable subscriptions

### **Best Practices**
1. **Isolation**: Each test should be independent
2. **Clarity**: Test names should describe the scenario
3. **Coverage**: Test both success and error paths
4. **Maintenance**: Keep tests simple and focused
5. **Documentation**: Comment complex test scenarios

---

*This testing infrastructure ensures safe refactoring with comprehensive coverage of critical functionality.*
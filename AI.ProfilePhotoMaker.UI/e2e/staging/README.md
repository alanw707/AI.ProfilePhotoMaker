# Staging Environment E2E Tests

Comprehensive end-to-end test suite for validating the AI Profile Photo Maker staging environment functionality.

## 🎯 Test Objectives

This test suite validates that the staging environment is fully functional with:

1. **Landing Page Functionality** - Verify page loads correctly with proper content
2. **Azure Blob Storage Integration** - Confirm real images load from Azure (not placeholders)
3. **Package Loading** - Test credit packages display with descriptions and pricing
4. **API Integration** - Verify all API endpoints work correctly
5. **Image Loading** - Confirm no more colored placeholders, real photos displayed
6. **Console Errors** - Validate no critical JavaScript errors
7. **Performance Metrics** - Ensure acceptable load times and responsiveness

## 📁 Test Structure

```
e2e/staging/
├── 01-landing-page.spec.ts      # Homepage functionality and basic loading
├── 02-package-functionality.spec.ts # Credit packages and pricing
├── 03-api-integration.spec.ts   # API endpoint validation
├── 04-image-loading.spec.ts     # Azure Blob Storage and image quality
├── 05-performance-metrics.spec.ts # Performance and Core Web Vitals
├── 06-comprehensive-report.spec.ts # Overall environment health report
├── utils/
│   └── test-helpers.ts          # Shared utility functions
└── README.md                    # This file
```

## 🚀 Running Tests

### Prerequisites

```bash
# Install Playwright browsers
npm run playwright:install
```

### Test Commands

```bash
# Run all staging tests
npm run test:e2e:staging

# Run tests with browser UI (visual debugging)
npm run test:e2e:staging:ui

# Run tests in headed mode (see browser)
npm run test:e2e:staging:headed

# Generate comprehensive report only
npm run test:e2e:staging:report

# Run specific browser tests
npm run test:e2e:chrome
npm run test:e2e:mobile

# Debug mode (step through tests)
npm run test:e2e:debug
```

## 📊 Test Coverage

### 🏠 Landing Page Tests (01-landing-page.spec.ts)
- ✅ Homepage loads without critical errors
- ✅ Style preview images from Azure Blob Storage
- ✅ Azure Blob Storage integration verification  
- ✅ Credit packages loading
- ✅ API endpoints validation
- ✅ Console error monitoring
- ✅ Responsive design testing
- ✅ Navigation and CTA functionality

### 💳 Package Functionality Tests (02-package-functionality.spec.ts)
- ✅ Credit packages load from API
- ✅ Package descriptions display
- ✅ Purchase buttons are functional
- ✅ Package data API integration
- ✅ Pricing information accuracy
- ✅ Error handling for package loading

### 🔌 API Integration Tests (03-api-integration.spec.ts)
- ✅ API endpoint accessibility
- ✅ Styles API endpoint validation
- ✅ Credit packages API endpoint
- ✅ CORS and security headers
- ✅ API response time measurement
- ✅ Error handling for failed requests

### 🖼️ Image Loading Tests (04-image-loading.spec.ts)
- ✅ Real images from Azure Blob Storage (not placeholders)
- ✅ Style preview image quality verification
- ✅ Image load performance metrics
- ✅ Image accessibility and alt text
- ✅ Broken image link detection
- ✅ Image optimization and format analysis

### ⚡ Performance Tests (05-performance-metrics.spec.ts)
- ✅ Core Web Vitals measurement (LCP, FID, CLS)
- ✅ Page load performance metrics
- ✅ Resource loading performance
- ✅ JavaScript performance analysis
- ✅ Mobile performance testing
- ✅ Performance anti-pattern detection

### 📋 Comprehensive Report (06-comprehensive-report.spec.ts)
- ✅ Overall environment health assessment
- ✅ Consolidated test results summary
- ✅ Issue identification and recommendations
- ✅ Performance metrics compilation
- ✅ JSON report generation

## 🎯 Critical Validation Points

### ✅ Must Pass
- [ ] **Real Images Loading**: Style previews must load actual photos from Azure Blob Storage
- [ ] **No Placeholders**: Less than 20% placeholder images
- [ ] **API Connectivity**: At least 70% API success rate
- [ ] **Page Load Time**: Under 5 seconds on desktop, 10 seconds on mobile
- [ ] **No Critical Errors**: Zero critical console errors that break functionality

### ⚠️ Should Pass (Warnings)
- [ ] **Package Descriptions**: All packages should have descriptions
- [ ] **Performance**: Core Web Vitals should meet Google standards
- [ ] **Image Optimization**: Modern formats (WebP) and reasonable file sizes
- [ ] **Accessibility**: Alt text coverage > 80%

## 📸 Screenshots & Evidence

Tests automatically capture screenshots at key points:
- `01-homepage-loaded.png` - Initial page state
- `02-style-previews.png` - Style showcase section
- `03-credit-packages.png` - Package pricing display
- `10-azure-blob-images.png` - Azure image verification
- `99-final-staging-state.png` - Final environment state

## 📋 Report Generation

The comprehensive test generates:
- **Console Output**: Real-time test progress and results
- **HTML Report**: `playwright-report/index.html`
- **JSON Report**: `staging-environment-report.json`
- **Screenshots**: Evidence in `screenshots/` directory

## 🐛 Common Issues & Troubleshooting

### High Placeholder Count
If tests show many placeholder images:
1. Check Azure Blob Storage connection
2. Verify style preview image upload process
3. Confirm Azure storage URLs in environment config

### API Failures
If API tests fail:
1. Verify staging API is running
2. Check CORS configuration
3. Confirm API endpoint URLs in environment.staging.ts

### Slow Performance
If performance tests fail:
1. Check Azure CDN configuration
2. Verify image optimization
3. Review bundle size and asset loading

## 🔄 Continuous Testing

These tests are designed to:
- Run in CI/CD pipelines
- Provide regression testing
- Monitor staging environment health
- Generate automated reports

## 📞 Support

For test failures or environment issues:
1. Check the HTML report for detailed failure information
2. Review screenshots for visual evidence
3. Examine the JSON report for programmatic analysis
4. Run individual test files for focused debugging

---

**Environment**: `https://aiprofilemaker-web-staging.thankfulriver-68674ea3.eastus2.azurecontainerapps.io`

**Last Updated**: January 2025
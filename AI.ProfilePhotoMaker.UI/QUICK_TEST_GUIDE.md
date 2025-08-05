# 🚀 Quick Test Guide - Staging Environment Validation

## Immediate Usage

```bash
# 1. Install Playwright browsers (first time only)
npm run playwright:install

# 2. Run comprehensive staging validation
npm run test:e2e:staging:report

# 3. View results
# - Console output: Real-time results and metrics
# - HTML report: open playwright-report/index.html  
# - JSON report: staging-environment-report.json
# - Screenshots: screenshots/ directory
```

## 🎯 Key Tests Performed

### ✅ Critical Validations
- **Real Images Loading**: Confirms Azure Blob Storage integration (no placeholders)
- **API Connectivity**: Validates backend services are functional  
- **Page Performance**: Ensures acceptable load times (< 5 seconds)
- **Package Loading**: Verifies credit packages display correctly
- **Console Errors**: Identifies critical JavaScript errors

### 📊 What You'll See

```bash
📋 COMPREHENSIVE STAGING ENVIRONMENT REPORT
===============================================================================
🕒 Generated: 2025-01-XX...
🌐 Environment: staging
🔗 Base URL: https://aiprofilemaker-web-staging...

📊 TEST RESULTS SUMMARY:
  ✅ landingPage: PASS
  ✅ azureIntegration: PASS  
  ✅ imageLoading: PASS
  ✅ packageFunctionality: PASS
  ✅ apiIntegration: PASS
  ✅ performance: PASS

💡 RECOMMENDATIONS:
  1. ✅ Staging environment is functioning well
  2. Consider monitoring performance metrics over time
  3. Set up automated testing for continuous validation
```

## 🚨 If Tests Fail

### Common Issues & Solutions

**🔴 Staging Environment Unreachable**
```
Solution: Verify staging deployment is running
Check: Azure Container Apps status
```

**🟡 High Placeholder Images**
```
Issue: Style previews showing colored placeholders instead of real photos
Solution: Upload real images to Azure Blob Storage
Check: Azure storage account and container configuration
```

**🟡 Package Loading Issues**  
```
Issue: Credit packages not displaying or missing descriptions
Solution: Verify package API endpoints and database content
Check: API connectivity and package data seeding
```

**🟡 API Failures**
```
Issue: Backend API endpoints returning errors
Solution: Check API service health and database connectivity
Check: CORS configuration and API authentication
```

## 📱 Mobile Testing

```bash
# Test mobile experience specifically
npm run test:e2e:mobile
```

## 🐛 Debug Mode

```bash
# Step through tests interactively
npm run test:e2e:debug

# Run with visible browser
npm run test:e2e:staging:headed

# Interactive UI mode
npm run test:e2e:staging:ui
```

## 📊 Expected Results

**✅ Healthy Staging Environment:**
- All tests pass with minimal warnings
- Real images load from Azure Blob Storage  
- Package descriptions display correctly
- API responses under 3 seconds
- No critical console errors

**⚠️ Issues to Address:**
- High placeholder image count (>20%)
- Slow API responses (>3 seconds)
- Missing package descriptions
- Critical JavaScript errors
- Poor mobile performance

---

**Quick Start**: `npm run test:e2e:staging:report`  
**Full Documentation**: See `e2e/staging/README.md`  
**Complete Summary**: See `STAGING_TEST_SUITE_SUMMARY.md`
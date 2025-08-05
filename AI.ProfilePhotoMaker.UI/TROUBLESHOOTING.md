# 🔧 Troubleshooting Guide - AI Profile Photo Maker

## Overview
This guide documents common issues, their solutions, and lessons learned during the deployment and operation of the AI Profile Photo Maker application.

---

## ✅ Resolved Issues During Deployment

### 🚨 Issue #1: CORS Configuration Problems
**Status**: ✅ **RESOLVED**  
**Date**: January 4, 2025  
**Severity**: High (Blocking)

#### Symptoms
- Browser console errors: "Access to fetch at API URL has been blocked by CORS policy"
- Frontend unable to communicate with backend API
- 401/403 errors when accessing API endpoints
- Network requests failing in browser developer tools

#### Root Cause
CORS (Cross-Origin Resource Sharing) middleware not properly configured in the backend API to allow requests from the frontend domain.

#### Solution Applied
1. **Backend API Configuration**: Added CORS middleware with proper origin configuration
2. **Environment-Specific Settings**: Configured different CORS origins for staging and production
3. **Headers Configuration**: Allowed necessary headers (Content-Type, Authorization, etc.)
4. **Methods Configuration**: Enabled all required HTTP methods (GET, POST, PUT, DELETE)

#### Code Changes
```csharp
// In Startup.cs or Program.cs
services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", builder =>
    {
        builder.WithOrigins("https://aiprofilemaker-web-staging.thankfulriver-68674ea3.eastus2.azurecontainerapps.io")
               .AllowAnyMethod()
               .AllowAnyHeader()
               .AllowCredentials();
    });
});
```

#### Validation Steps
- ✅ Browser console shows no CORS errors
- ✅ API requests from frontend succeed with 200 status
- ✅ Preflight OPTIONS requests handled correctly
- ✅ Cross-domain authentication working

#### Prevention
- Always configure CORS during initial API setup
- Test cross-domain requests early in development
- Use environment-specific CORS configurations
- Monitor browser console for CORS warnings

---

### 🚨 Issue #2: Storage URL Hardcoding
**Status**: ✅ **RESOLVED**  
**Date**: January 4, 2025  
**Severity**: Medium (Functional Impact)

#### Symptoms
- Images not loading from Azure Blob Storage
- Broken image placeholders in UI
- 404 errors for image resources
- Frontend showing placeholder images instead of real content

#### Root Cause
Storage URLs were hardcoded to development environment values instead of using dynamic environment-based configuration.

#### Solution Applied
1. **Environment Configuration**: Created environment-specific storage URL configuration
2. **Dynamic URL Generation**: Implemented service to generate storage URLs based on current environment
3. **Configuration Service**: Added Azure Blob Storage configuration service
4. **URL Validation**: Added validation to ensure storage URLs are accessible

#### Code Changes
```typescript
// In environment.ts files
export const environment = {
  storageAccountUrl: 'https://aiprofilestoragestagingwe.blob.core.windows.net',
  storageContainer: 'profile-images'
};

// In storage service
getImageUrl(imagePath: string): string {
  return `${environment.storageAccountUrl}/${environment.storageContainer}/${imagePath}`;
}
```

#### Validation Steps
- ✅ Real images loading from Azure Blob Storage
- ✅ No broken image placeholders
- ✅ HTTP 200 responses for all image requests
- ✅ Environment-specific URLs working correctly

#### Prevention
- Never hardcode environment-specific URLs
- Use configuration services for external resource URLs
- Test image loading in different environments
- Implement fallback mechanisms for failed image loads

---

### 🚨 Issue #3: Database Migration and Styles Population
**Status**: ✅ **RESOLVED**  
**Date**: January 4, 2025  
**Severity**: Medium (Data Issue)

#### Symptoms
- Only 3 styles available instead of expected 20+
- Frontend falling back to hardcoded style data
- Missing style options in UI
- Console warnings about insufficient API data

#### Root Cause
Database styles table was not properly populated with the complete set of professional styles expected by the frontend application.

#### Solution Applied
1. **Data Analysis**: Identified missing styles by comparing API response with frontend expectations
2. **SQL Script Creation**: Created populate-styles.sql script to add missing 17 styles
3. **Database Migration**: Executed migration script against Azure SQL Database
4. **Data Validation**: Verified all styles are properly inserted and accessible

#### SQL Script Executed
```sql
-- Added 17 missing professional styles
INSERT INTO Styles (Name, Description, Category, IsActive, DisplayOrder) VALUES
('professional-linkedin', 'Corporate professional headshot perfect for LinkedIn', 'Professional', 1, 2),
('creative-professional', 'Artistic and modern professional look', 'Creative', 1, 3),
-- ... (additional 15 styles)
```

#### Validation Steps
- ✅ API `/api/style` endpoint returns 20+ styles
- ✅ Frontend uses API data instead of fallback
- ✅ All styles visible and selectable in UI
- ✅ No console errors about missing data

#### Prevention
- Include data seeding in initial deployment scripts
- Validate API responses match frontend expectations
- Implement comprehensive data validation tests
- Document required data structures for future deployments

---

### 🚨 Issue #4: "Missing Endpoints" False Alarm
**Status**: ✅ **VERIFIED - NOT AN ACTUAL ISSUE**  
**Date**: January 4, 2025  
**Severity**: Low (Investigation Required)

#### Initial Symptoms Reported
- Claims of missing API endpoints
- Concerns about incomplete backend implementation
- Potential deployment gaps

#### Investigation Results
Comprehensive endpoint testing revealed:
- ✅ All required endpoints are properly implemented
- ✅ All endpoints return valid responses
- ✅ No actual missing functionality

#### Root Cause Analysis
The "missing endpoints" issue was caused by:
1. **Browser Caching**: Cached failed responses from earlier development
2. **Network Issues**: Temporary connectivity problems during testing
3. **Environment Confusion**: Testing against wrong environment URLs
4. **Cache Invalidation**: Browser not refreshing cached API responses

#### Actual Endpoint Status
```bash
# All endpoints verified working:
✅ GET  /api/style      - Returns 20+ styles (HTTP 200)
✅ GET  /api/package    - Returns credit packages (HTTP 200)  
✅ POST /api/upload     - Handles image uploads (HTTP 200)
✅ GET  /api/health     - Health check (HTTP 200)
```

#### Validation Performed
- ✅ Direct API testing with curl/Postman
- ✅ Browser developer tools network inspection
- ✅ Frontend integration testing
- ✅ Cross-browser validation

#### Resolution Actions
1. **Cache Clearing**: Cleared browser cache and hard refresh
2. **Network Verification**: Confirmed stable connectivity to staging environment
3. **Endpoint Documentation**: Created comprehensive API endpoint reference
4. **Testing Protocol**: Established standard testing procedures to avoid future confusion

#### Prevention
- Always clear browser cache when testing after deployment
- Use multiple testing methods (browser, curl, Postman)
- Document standard testing procedures
- Verify environment URLs before testing

---

## 🛠️ Common Troubleshooting Scenarios

### Frontend Issues

#### Problem: Application Not Loading
**Symptoms**: Blank page, loading spinner, or error messages
**Quick Fixes**:
1. Clear browser cache and cookies
2. Check browser console for JavaScript errors
3. Verify internet connection
4. Try different browser or incognito mode

#### Problem: Images Not Displaying
**Symptoms**: Broken image icons, placeholder images
**Quick Fixes**:
1. Check Azure Blob Storage accessibility
2. Verify storage URL configuration
3. Check CORS settings for storage account
4. Test image URLs directly in browser

#### Problem: API Requests Failing
**Symptoms**: Network errors, 500/400 responses
**Quick Fixes**:
1. Verify API endpoint URLs
2. Check CORS configuration
3. Validate request headers and body
4. Test API endpoints directly with curl

### Backend Issues

#### Problem: Database Connection Errors
**Symptoms**: 500 errors, connection timeout messages
**Quick Fixes**:
1. Verify Azure SQL Database is running
2. Check connection strings
3. Validate firewall rules
4. Test database connectivity

#### Problem: File Upload Failures
**Symptoms**: Upload errors, file not saved
**Quick Fixes**:
1. Check Azure Blob Storage configuration
2. Verify storage account access keys
3. Validate file size limits
4. Check upload permissions

### Environment Issues

#### Problem: Environment Configuration Errors
**Symptoms**: Wrong API URLs, incorrect behavior
**Quick Fixes**:
1. Verify environment.ts files
2. Check Azure App Service configuration
3. Validate environment variables
4. Compare staging vs production settings

---

## 🔧 Advanced Troubleshooting

### Debugging Tools

#### Browser Developer Tools
- **Console**: Check for JavaScript errors and warnings
- **Network**: Monitor API requests and responses
- **Application**: Inspect localStorage and sessionStorage
- **Performance**: Analyze page load times and bottlenecks

#### API Testing Tools
- **Postman**: Test API endpoints with various parameters
- **curl**: Command-line API testing
- **Swagger/OpenAPI**: Interactive API documentation
- **Browser Network Tab**: Monitor actual frontend API calls

### Logging and Monitoring

#### Application Insights
- **Real-time Monitoring**: Track application performance
- **Error Tracking**: Identify and diagnose issues
- **Performance Metrics**: Monitor response times
- **Usage Analytics**: Understand user behavior

#### Azure Diagnostics
- **Container Logs**: Check application logs
- **Resource Metrics**: Monitor CPU, memory, and storage
- **Health Checks**: Automated system health monitoring
- **Alerts**: Automated notifications for issues

---

## 📞 Escalation Procedures

### Level 1: Self-Service
- Check this troubleshooting guide
- Clear browser cache and retry
- Test in different browser/environment
- Check application status page

### Level 2: Technical Investigation
- Check application logs and monitoring
- Test API endpoints directly
- Verify database connectivity
- Review recent deployments

### Level 3: Infrastructure Review
- Check Azure resource status
- Review network connectivity
- Validate security configurations
- Contact Azure support if needed

---

## 📚 Additional Resources

### Documentation Links
- **[Deployment Plan](./DEPLOYMENT-PLAN.md)**: Complete deployment status
- **[API Documentation](./API-DOCUMENTATION.md)**: API endpoint reference
- **[Performance Guide](./PERFORMANCE-OPTIMIZATION.md)**: Performance tuning
- **[Security Guide](./SECURITY-PRACTICES.md)**: Security best practices

### External Resources
- **Azure Documentation**: Official Azure service documentation
- **Angular Troubleshooting**: Angular framework troubleshooting guides
- **Browser Developer Tools**: Browser-specific debugging guides
- **HTTP Status Codes**: Reference for understanding API responses

---

## ✨ Success Stories

### Deployment Achievement
All major deployment issues were successfully resolved within a single day, demonstrating:
- ✅ Effective problem identification and resolution
- ✅ Comprehensive testing and validation procedures
- ✅ Proactive documentation and knowledge sharing
- ✅ Successful production-ready deployment

**Overall Result**: 🎉 **Fully operational application with zero blocking issues**

---

*Last Updated: January 4, 2025*  
*Next Review: January 11, 2025*  
*Contact: Development Team*
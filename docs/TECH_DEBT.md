# Technical Debt Tracker

This document tracks technical debt items that should be addressed in future development cycles.

## Priority Levels
- 🔴 **High**: Performance impact or blocking issues
- 🟡 **Medium**: Code quality or maintainability issues  
- 🟢 **Low**: Nice-to-have improvements

---

## Active Tech Debt Items

### 🟡 Zone.js setTimeout Violations from face-api.js
**Date Added**: 2025-07-03  
**Component**: Face Detection Service  
**Issue**: Console warnings for setTimeout operations from face-api.js library  

**Description**:
- Face detection functionality works correctly
- Console shows "[Violation] 'setTimeout' handler took Xms" warnings
- Operations range from 52ms to 1256ms
- Currently using zone.js blacklist configuration but doesn't catch all internal library operations

**Current Status**:
- ✅ Face detection works correctly
- ✅ Image upload functionality restored
- ❌ Console warnings persist

**Proposed Solutions**:
1. **Web Worker Implementation**: Move face-api.js to dedicated worker thread
2. **Enhanced Zone.js Config**: More comprehensive blacklist configuration
3. **Alternative Library**: Replace face-api.js with lighter detection library

**Impact**: Low - functionality works, only affects console cleanliness

**Estimated Effort**: 4-6 hours for Web Worker implementation

---

## Resolved Items

### ✅ Upload Interface Disappearing After Face Detection
**Date Resolved**: 2025-07-03  
**Issue**: Dashboard incorrectly advanced to Step 2 when images passed validation  
**Solution**: Removed premature `uploadedImages` state update during file selection

### ✅ Database UNIQUE Constraint Violation on ProcessedImageUrl
**Date Resolved**: 2025-07-03  
**Issue**: Multiple uploads failed due to empty string ProcessedImageUrl values  
**Solution**: Set ProcessedImageUrl to unique file URL for uploaded images

---

## Guidelines for Tech Debt Management

1. **Document When Adding**: Always add context and proposed solutions
2. **Regular Review**: Review quarterly during sprint planning
3. **Impact Assessment**: Consider user impact vs development time
4. **Incremental Fixes**: Address during related feature work when possible
5. **Remove When Resolved**: Move to resolved section with solution details
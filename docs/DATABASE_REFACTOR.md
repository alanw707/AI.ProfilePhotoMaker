# Database Refactoring: Model Information Normalization

## Current Issues

The system currently stores model information in two separate tables, violating database normalization:

1. **UserProfile** table contains:
   - `TrainedModelId` (string)
   - `TrainedModelVersionId` (string) 
   - `ModelTrainedAt` (DateTime)

2. **ModelCreationRequest** table contains:
   - `UserId` (string)
   - `ReplicateModelId` (string)
   - `TrainedModelVersion` (string)
   - `Status` (enum: Pending, Creating, Ready, Failed)
   - `CompletedAt` (DateTime)

## Problems This Causes

1. **Data Inconsistency**: Model can exist in ModelCreationRequest but not in UserProfile
2. **Update Complexity**: Need to update two tables when model status changes
3. **Query Complexity**: Need to check multiple sources to determine if user has a model
4. **Sync Issues**: Current workarounds involve complex sync logic between tables

## Proposed Solution

### Option 1: Single Source of Truth (Recommended)

Remove model fields from UserProfile and use ModelCreationRequest as the single source:

```sql
-- Remove from UserProfile
ALTER TABLE UserProfile DROP COLUMN TrainedModelId;
ALTER TABLE UserProfile DROP COLUMN TrainedModelVersionId;
ALTER TABLE UserProfile DROP COLUMN ModelTrainedAt;

-- Add index for performance
CREATE INDEX IX_ModelCreationRequest_UserId_Status 
ON ModelCreationRequest(UserId, Status);
```

**Benefits:**
- Single source of truth
- No sync issues
- Supports multiple models per user naturally
- Cleaner separation of concerns

**Code Changes Required:**
1. Update ProfileController to query ModelCreationRequest
2. Update dashboard to check ModelCreationRequest for model status
3. Remove all sync logic from DashboardStateService

### Option 2: Foreign Key Relationship

Add foreign key from UserProfile to ModelCreationRequest:

```sql
-- Add to UserProfile
ALTER TABLE UserProfile ADD ActiveModelId varchar(450);
ALTER TABLE UserProfile ADD CONSTRAINT FK_UserProfile_ModelCreationRequest 
    FOREIGN KEY (ActiveModelId) REFERENCES ModelCreationRequest(Id);
```

**Benefits:**
- Quick access to active model
- Supports model switching in future
- Maintains referential integrity

### Option 3: Denormalized for Performance

Keep both but establish clear ownership:
- ModelCreationRequest is source of truth
- UserProfile fields are cache/denormalized view
- Background job maintains consistency

## Recommendation

**Go with Option 1** - Use ModelCreationRequest as single source of truth:

1. It's the cleanest solution
2. Already tracks all necessary information
3. Supports future features (multiple models, model history)
4. Eliminates sync issues completely

## Implementation Steps

1. Create new migration to remove fields from UserProfile
2. Update repository methods to query ModelCreationRequest
3. Update DTOs to include model information from joins
4. Remove all sync logic and workarounds
5. Update frontend to use new API responses

## Example Query Pattern

```csharp
// Get user profile with active model
var profile = await _context.UserProfiles
    .Include(p => p.User)
    .Where(p => p.UserId == userId)
    .Select(p => new UserProfileDto
    {
        Id = p.Id,
        FirstName = p.FirstName,
        LastName = p.LastName,
        // Get model info from ModelCreationRequest
        TrainedModelId = _context.ModelCreationRequests
            .Where(m => m.UserId == userId && m.Status == ModelCreationStatus.Ready)
            .OrderByDescending(m => m.CompletedAt)
            .Select(m => m.ReplicateModelId)
            .FirstOrDefault(),
        TrainedModelVersionId = _context.ModelCreationRequests
            .Where(m => m.UserId == userId && m.Status == ModelCreationStatus.Ready)
            .OrderByDescending(m => m.CompletedAt)
            .Select(m => m.TrainedModelVersion)
            .FirstOrDefault()
    })
    .FirstOrDefaultAsync();
```

## Migration Strategy

1. Deploy code that reads from both sources (current state)
2. Run migration to remove UserProfile fields
3. Deploy code that only reads from ModelCreationRequest
4. Remove all sync/workaround code

This ensures zero downtime and safe rollback if needed.
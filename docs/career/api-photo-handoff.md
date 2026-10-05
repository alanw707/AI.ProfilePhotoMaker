# Career API: optional photo handoff (#391)

Same envelope, auth, flag (403 `CareerWorkspaceDisabled`) and error shape as `api-profile-goal.md`. None of these endpoints spend credits, consume package allowances or start generation.

## List

`GET /api/career/photos` → 200:

```json
{
  "photos": [
    { "id": 42, "imageUrl": "https://…", "createdAt": "…", "style": "linkedin",
      "isWatermarkedPreview": false }        // true: free preview, raw withheld; cannot be chosen
  ],                                         // owner's succeeded generated images, newest first, max 24
  "selectedPhotoId": 42,                     // null when none chosen
  "selectedPhotoAvailable": true,            // false when the chosen photo no longer exists or is no longer eligible
  "entitlements": [                          // read-only copy of active photo packages
    { "packageCode": "starter_package", "packageName": "Starter Package",
      "remainingCandidates": 2, "remainingRefinements": 3, "remainingPremiumAugmentations": 0,
      "platformExportKitAvailable": true, "expiresAt": null }
  ]
}
```

Original uploads, failed/abandoned generations and private raw paths are never listed.

## Choose / stop using

`PUT /api/career/photos/selection` with `{ "processedImageId": 42 }` → 200 `{ "selectedPhotoId": 42, "careerGoalId": "guid|null", "selectedAt": "…" }`.

| Status | Code | When |
|---|---|---|
| 400 | `ValidationError` (`fieldErrors.processedImageId`) | missing or not positive |
| 404 | `CareerPhotoNotFound` | not the caller's, missing, not a succeeded generated image, or an original upload |
| 409 | `CareerPhotoIsPreview` | a watermarked free preview (improve it in the photo workspace first) |

`DELETE /api/career/photos/selection` → 204 (idempotent).

## Workspace handoff (UI only)

Links into the photo workspace carry `careerReturn` and `careerGoal`:

- Improve: `/app/enhance?refineImageId=<id>&careerReturn=materials&careerGoal=<goal guid>`
- Create: `/app/enhance?careerReturn=materials&careerGoal=<goal guid>`

`careerReturn` is a key resolved by one allowlist: `materials` → `/app/career/materials`, `profile` → `/app/career/profile`, `home` → `/app/career`. Any other value, or a `careerGoal` that is not a GUID, is ignored (no back link). The workspace keeps both parameters in the pricing `returnUrl`, so they survive checkout. On return, the materials page compares `careerGoal` with the current goal and says so if the goal changed meanwhile.

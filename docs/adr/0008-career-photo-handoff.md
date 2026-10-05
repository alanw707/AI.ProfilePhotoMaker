# ADR 0008: Career photos are an optional handoff to the existing photo workspace

Status: accepted (2026-10-04, ticket #391, spec #376)

## Context

#391 lets a user with a career goal reuse a photo they own, or improve/create one in the existing photo workspace, then come back to the same career goal. Photo purchase, generation, refinement, entitlements and checkout-return already exist and are the authority (spec #376: "Existing photo endpoints remain authority for packages, generation, payment and download; career tools cannot bypass their checks").

## Decision

- **Career never spends.** The career API only reads photo data and stores which owned photo the user chose. It never calls generation, refinement, purchase or entitlement-consuming code, and never reserves career quota for photo work. Retries and returns reuse the photo workspace's own idempotency and fulfilment.
- **Owned, delivered photos only.** `GET /api/career/photos` lists the owner's successful generated images (`IsGenerated`, `GenerationStatus = succeeded`), newest first, max 24. A free preview whose raw asset is still withheld (`RawImageStoragePath` set, ADR 0005) is listed as `isWatermarkedPreview: true` and cannot be chosen; the user can only take it to the workspace. Raw/private paths are never returned.
- **Choosing a photo** (`PUT /api/career/photos/selection`) stores one `CareerPhotoSelection` per owner (processed image id, goal id at the time, timestamp). Another owner's image, a missing image, an unsuccessful image or the user's original upload answers 404 `CareerPhotoNotFound`; a watermarked preview answers 409 `CareerPhotoIsPreview`. No foreign key to `ProcessedImages`: if the photo is later deleted, reads report `selectedPhotoAvailable: false`. The selection joins `CareerPrivateDataService` deletion coverage.
- **Return route is a key, never a URL.** The workspace link carries `careerReturn=materials|profile|home` and `careerGoal=<guid>`. A single resolver maps the key to a fixed in-app path; anything else (absolute or protocol-relative URLs, `javascript:`, unknown keys, malformed GUIDs) is ignored and no back link is shown. The pricing page already preserves `/app/enhance` query strings through checkout, so the key survives a purchase.
- **Disclosure before paid action stays in the workspace.** The materials page shows current package allowances read-only and states that opening the workspace is free and that any purchase or generation happens there after the user confirms. Declining a purchase changes nothing in career data.
- **Expired session.** A 401 from a career endpoint sends the user to sign in with `returnUrl=/app/career/materials`.

## Consequences

- The photo workspace gains only a small, optional "Back to career materials" banner when a valid key is present; its purchase, preview and recovery behaviour is unchanged and its existing tests must pass unchanged.
- A real career materials page (resume drafts, exports) arrives with #389/#390; this slice adds the route with the photo section only.

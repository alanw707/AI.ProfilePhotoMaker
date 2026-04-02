# Feature Spec: Reduce Minimum Image Upload Requirement to 5
**Branch:** `feature/min-images-5`
**Date:** 2026-04-02
**Status:** In Progress

---

## Goal
Lower the minimum image upload requirement from **10** to **5** across the entire codebase — backend validation, frontend UI/UX, marketing copy, and email copy.

## Background
- The .NET 10 refactor is complete (merged to main 2026-04-02)
- Lowering the barrier to entry reduces drop-off at the upload step
- "5 minimum, 10 recommended, 15 for best results" is the new messaging hierarchy

---

## Tasks

### ✅ Backend — API (C#)

- [x] `Controllers/ProfileController.cs`
  - `CanStartTraining` threshold: `>= 10` → `>= 5`
  - Status switch: `< 10` / `>= 10` → `< 5` / `>= 5`
  - Messages: "Need at least 10 images" → "Need at least 5 images"

- [x] `Controllers/ModelStatusController.cs`
  - `canStartTraining` threshold: `>= 10` → `>= 5`
  - Status message: "Need at least 10 images" → "Need at least 5 images"

- [x] `Controllers/ImageController.cs`
  - ZIP creation guard (`imageFiles.Count < 10`) → `< 5` (+ log message)
  - Valid images guard (`validImages.Count < 10`) → `< 5` (+ log message)
  - Training start guard (`uploadedImages.Count < 10`) → `< 5` (+ error response message)

- [x] `Services/Notifications/EmailNotificationService.cs`
  - "10+ for best results" → "5+ for best results"
  - "Just upload 10–20 selfies" → "Just upload 5–15 selfies"

- [x] `Data/UserProfileRepository.cs`
  - Code comment: "minimum 10 images" → "minimum 5 images"

- [ ] **Verify `Controllers/TrainingController.cs`** (new file from refactor — confirmed clean, no image count validation)
- [ ] **Verify `Controllers/GenerationController.cs`** (new file — no training image count validation expected)

---

### ✅ Frontend — Angular UI (TypeScript / HTML)

- [x] `components/dashboard/file-upload-section/file-upload-section.component.html`
  - Progress bar thresholds: red `< 10` / yellow `>= 10 && < 15` → red `< 5` / yellow `>= 5 && < 10`
  - Milestone markers: "10 - Minimum required" (50%) → "5 - Minimum required" (33%), "15 - Recommended" (75%) → "10 - Recommended" (67%)
  - Status indicators: `>= 10` / `< 10` → `>= 5` / `< 5`
  - Badge text: "10 minimum" → "5 minimum", "15 recommended" → "10 recommended"

- [x] `components/dashboard/style-selector/style-selector.component.html`
  - Button disabled guard: `uploadedImageCount < 10` → `< 5`
  - Training note `*ngIf`: `uploadedImageCount < 10` → `< 5`
  - Training note text: "Upload at least 10 images" → "Upload at least 5 images"
  - Credits check `*ngIf`: `uploadedImageCount >= 10` → `>= 5`

- [x] `dashboard/dashboard.component.ts`
  - Thumbnail length check: `>= 10` → `>= 5`

- [x] `dashboard/dashboard.component.html`
  - Copy: "Upload 10+ selfies (15 recommended, 20 for best results)" → "Upload 5+ selfies (10 recommended, 15 for best results)"

- [x] `services/workflow-orchestration.service.ts`
  - Error message: "at least 10 clear photos" → "at least 5 clear photos"

---

### ✅ Frontend — Marketing / Landing Copy

- [x] `pages/landing/landing.component.html`
  - "10–20 photos" → "5–15 photos" (×2)

- [x] `pages/premium/premium.component.ts`
  - "Upload 10-20 high-quality selfies" → "Upload 5-15 high-quality selfies"

- [x] `pages/marketing/seo-pages.records.part1a.data.ts`
  - `{ value: 'At least 10', label: 'Minimum selfies' }` → `At least 5`
  - "Upload at least 10 clear selfies" (title, ×2) → `at least 5`
  - "We recommend at least 10 clear selfies" → `at least 5`

- [x] `pages/marketing/seo-pages.records.part1c.data.ts`
  - "Upload at least 10 photos, pick styles..." (×2) → `at least 5`
  - "Upload 10–20 clear selfies" → "Upload 5–15 clear selfies"
  - "We recommend 10–20 clear selfies" → "We recommend 5–15 clear selfies"

---

### 🔲 Remaining / To Verify

- [x] **Check test files** for any `10` assertions tied to upload minimum
  - `ModelStatusIntegrationTests.cs` line 94: seeds 10 images, asserts `totalUploadedImages >= 10` — **safe, won't break** (10 still satisfies >= 5 threshold; assertion tests seeded count, not minimum)
  - All other `10` refs in tests are timeouts, retry loops, credits, pricing — unrelated ✅
- [ ] **Run tests locally** to confirm no regressions
- [ ] **Commit all changes** to `feature/min-images-5`
- [ ] **Push branch** to origin
- [ ] **Open PR** → main

---

## Files Changed Summary
| File | Change |
|------|--------|
| `API/Controllers/ProfileController.cs` | threshold + messages |
| `API/Controllers/ModelStatusController.cs` | threshold + messages |
| `API/Controllers/ImageController.cs` | 3× guards + messages |
| `API/Services/Notifications/EmailNotificationService.cs` | 2× email copy |
| `API/Data/UserProfileRepository.cs` | comment |
| `UI/.../file-upload-section.component.html` | thresholds + milestones + badges |
| `UI/.../style-selector.component.html` | 3× guards + copy |
| `UI/dashboard/dashboard.component.ts` | threshold |
| `UI/dashboard/dashboard.component.html` | copy |
| `UI/services/workflow-orchestration.service.ts` | error message |
| `UI/pages/landing/landing.component.html` | 2× copy |
| `UI/pages/premium/premium.component.ts` | copy |
| `UI/pages/marketing/seo-pages.records.part1a.data.ts` | 4× copy |
| `UI/pages/marketing/seo-pages.records.part1c.data.ts` | 4× copy |

**Total: 14 files, ~25 individual changes**

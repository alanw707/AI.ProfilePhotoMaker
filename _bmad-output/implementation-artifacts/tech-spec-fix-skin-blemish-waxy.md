---
title: 'Fix Skin Quality - Remove Blemish Artifacts and Waxy Forehead'
slug: 'fix-skin-blemish-waxy'
created: '2026-02-18'
status: 'done'
stepsCompleted: [1, 2, 3, 4, 5, 6, 7, 8]
tech_stack: ['C#', '.NET 8', 'xUnit', 'Moq', 'EF Core 8 Migrations', 'SQL Server', 'Replicate FLUX API']
files_to_modify:
  - 'AI.ProfilePhotoMaker.API/Services/ImageProcessing/ReplicateApiClient.cs'
  - 'AI.ProfilePhotoMaker.API/Services/ImageProcessing/MockReplicateApiClient.cs'
  - 'AI.ProfilePhotoMaker.API/Migrations/20260218235353_FixSkinBlemishAndWaxyForehead.cs (new)'
  - 'AI.ProfilePhotoMaker.API/Migrations/20260218235353_FixSkinBlemishAndWaxyForehead.Designer.cs (auto-generated)'
  - 'AI.ProfilePhotoMaker.API/Migrations/ApplicationDbContextModelSnapshot.cs (seed timestamp update)'
  - 'AI.ProfilePhotoMaker.API.Tests/Unit/ReplicateApiClientStyleTuningTests.cs'
  - 'AI.ProfilePhotoMaker.API.Tests/Unit/ReplicateApiClientNegativePromptTests.cs'
code_patterns: ['EF Core raw SQL migration (REPLACE/UPDATE pattern)', 'private static readonly string[] modifier pool', 'static private method for negative prompt assembly']
test_patterns: ['xUnit [Fact]/[Theory]', 'Moq HttpMessageHandler', 'InMemory EF DbContext', 'Assert.Contains / Assert.DoesNotContain']
---

# Tech-Spec: Fix Skin Quality - Remove Blemish Artifacts and Waxy Forehead

**Created:** 2026-02-18

## Overview

### Problem Statement

Generated profile photos exhibit two related skin quality defects across all styles:

1. **Moles / dark spot artifacts** — The AI renders visible moles and dark spots on subjects' faces. Root cause: `"subtle skin pores"` is in the `s_realismPromptModifiers` array in `ReplicateApiClient.cs`. This modifier is randomly injected into positive prompts at runtime and instructs the diffusion model to render fine skin surface detail — which causes it to hallucinate moles, birthmarks, and blemishes not present in the user's training photos. The defect occurs randomly (whichever generation gets this modifier drawn).

2. **Waxy / plastic forehead** — In bright sunlit or high-key lighting styles the skin on the forehead renders as shiny/plastic. Root cause: the current skin negative prompt (`waxy skin, plastic skin, ...`) lacks explicit forehead-specific highlight terms. The model isn't being told strongly enough to avoid specular skin gleam under bright lighting conditions.

### Solution

Two-pronged fix applied at two layers of the stack:

1. **Runtime code changes** (`ReplicateApiClient.cs` + `MockReplicateApiClient.cs`):
   - Replace `"subtle skin pores"` with `"soft natural finish"` in the `s_realismPromptModifiers` pool — eliminates the blemish-triggering modifier while preserving realism intent.
   - In `CreateFluxNegativePrompt`, universally append `, moles, dark spots, skin blemishes, birthmarks, skin spots` to every non-empty negative prompt at runtime — belt-and-suspenders guard regardless of DB style content.

2. **DB migration** — Strengthen the `NegativePromptTemplate` for all active styles that carry the skin realism segment, adding forehead-specific waxy terms (`oily forehead, shiny forehead, specular highlights on skin, skin gleam`) and blemish terms (`moles, dark spots, skin blemishes, birthmarks`) directly into the stored DB strings.

### Scope

**In Scope:**
- Replace `"subtle skin pores"` → `"soft natural finish"` in `s_realismPromptModifiers` in `ReplicateApiClient.cs`
- Append blemish suffix in `CreateFluxNegativePrompt` in `ReplicateApiClient.cs`
- Mirror the same `CreateFluxNegativePrompt` change in `MockReplicateApiClient.cs` (duplicate method)
- New EF Core migration strengthening `NegativePromptTemplate` for all active styles where `NegativePromptTemplate LIKE '%waxy skin%'`
- Update two existing unit tests that will break from the code changes
- Add three new unit tests: blemish suffix on non-empty template, empty template edge case (AC3), and modifier pool regression

**Out of Scope:**
- Changing guidance scale / inference steps
- Modifying positive `PromptTemplate` columns (the "healthy natural skin" language added in `SoftenSkinRealismConstraints` is adequate)
- Styles that don't carry the skin realism negative block (e.g. `digital-native`, `fitness`, `glamour`) — intentionally excluded, consistent with prior migrations
- Any frontend changes
- Refactoring the `CreateFluxNegativePrompt` duplication between the real and mock clients (noted for future cleanup)

---

## Context for Development

### Codebase Patterns

**Prompt assembly pipeline** (executed on every generation in `GenerateImagesAsync` and `GenerateBaseStylePreviewAsync`):
1. `GetStylePromptsFromDatabase(style)` — fetches `PromptTemplate` + `NegativePromptTemplate` from `dbo.Styles` via EF
2. `CreateFluxStylePrompt(...)` — replaces `{subject}`, `{gender}`, `{ethnicity}` placeholders in the positive template
3. `CreateFluxNegativePrompt(...)` — replaces the same placeholders in the negative template
4. `ApplyRealismModifier(stylePrompt)` — randomly appends one item from `s_realismPromptModifiers` to the positive prompt
5. Final `prompt` + `negative_prompt` sent to Replicate prediction API

**`s_realismPromptModifiers`** — `private static readonly string[]` at `ReplicateApiClient.cs` lines 24–32. Current 6 values:
```csharp
"natural skin texture",
"subtle skin pores",       // <-- REMOVE / REPLACE THIS
"soft natural sheen",
"realistic skin detail",
"unretouched look",
"candid lighting"
```

**`ApplyRealismModifier`** — confirmed signature: `internal static string ApplyRealismModifier(string prompt, Random? random = null)` at line 1006 in `ReplicateApiClient.cs`. It is `internal static`, so test projects can call it directly as `ReplicateApiClient.ApplyRealismModifier(prompt, new Random(seed))` without any reflection or test helpers.

**`CreateFluxNegativePrompt`** — `private static string` method at line ~961 in `ReplicateApiClient.cs`. Pattern:
```csharp
if (string.IsNullOrWhiteSpace(negativePromptTemplate)) return string.Empty;
// ... placeholder replacements ...
return result.Replace("  ", " ").Trim();
```
The blemish suffix must be appended AFTER the `.Replace("  ", " ").Trim()` cleanup, before the final `return`, guarded by `if (!string.IsNullOrWhiteSpace(result))`.

**`MockReplicateApiClient.CreateFluxNegativePrompt`** — verbatim duplicate at line ~342 in `MockReplicateApiClient.cs`. Identical change must be applied.

**EF Core migration pattern** — all skin migrations use raw SQL via `migrationBuilder.Sql(...)`. The `REPLACE()` SQL function approach (used in `SoftenSkinRealismConstraints`) is the established pattern:
```sql
UPDATE dbo.Styles
SET NegativePromptTemplate = REPLACE(NegativePromptTemplate, 'old segment', 'new segment'),
    UpdatedAt = GETUTCDATE()
WHERE IsActive = 1
  AND NegativePromptTemplate LIKE '%waxy skin%';
```

**Migration workflow** — always `dotnet ef migrations add {Name}` first to generate the auto-scaffolded `.cs` + `.Designer.cs`, then fill in `Up()` / `Down()` SQL bodies. Never manually create `.Designer.cs`.

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `AI.ProfilePhotoMaker.API/Services/ImageProcessing/ReplicateApiClient.cs` | Lines 24–32: `s_realismPromptModifiers`. Lines 961–982: `CreateFluxNegativePrompt`. Lines 1006–1026: `ApplyRealismModifier`. |
| `AI.ProfilePhotoMaker.API/Services/ImageProcessing/MockReplicateApiClient.cs` | Lines 342–363: duplicate `CreateFluxNegativePrompt` — must mirror the real client change. |
| `AI.ProfilePhotoMaker.API/Migrations/20260111095443_SoftenSkinRealismConstraints.cs` | Pattern reference for Up/Down SQL with REPLACE strategy and rollback constants. |
| `AI.ProfilePhotoMaker.API/Migrations/20260216131026_AddAdminPanelEntities.cs` | Most recent migration — run `dotnet ef migrations add` after this one. |
| `AI.ProfilePhotoMaker.API/Migrations/ApplicationDbContextModelSnapshot.cs` | Auto-updated by `dotnet ef migrations add` — do NOT edit manually. |
| `AI.ProfilePhotoMaker.API.Tests/Unit/ReplicateApiClientStyleTuningTests.cs` | Lines 163–175: `allowedModifiers` array hardcodes `"subtle skin pores"` — must be updated. |
| `AI.ProfilePhotoMaker.API.Tests/Unit/ReplicateApiClientNegativePromptTests.cs` | Line 97: exact negative prompt assertion — must include blemish suffix after code change. |

### Technical Decisions

- **Belt-and-suspenders (code + DB):** The DB migration updates stored template strings, but `CreateFluxNegativePrompt` is the single runtime choke-point that guarantees blemish terms are always sent regardless of DB content or future style seeds.
- **Replace not remove `"subtle skin pores"`:** Keeps modifier pool size stable; `"soft natural finish"` encourages texture realism without triggering blemish hallucination.
- **Migration scope:** Only rows where `NegativePromptTemplate LIKE '%waxy skin%'` — consistent with all prior skin migrations; intentionally excludes styles with minimal/different negative prompt language.
- **Do NOT re-add `poreless skin` or `exaggerated wrinkles`:** These were explicitly removed in `SoftenSkinRealismConstraints` because they caused dry/aged appearance. The new migration must not reintroduce them.
- **`MockReplicateApiClient` duplication is pre-existing tech debt** — fixing the duplication (e.g. extracting to a shared static helper) is out of scope but should be a follow-up.

---

## Implementation Plan

### Tasks

- [ ] **Task 1: Replace `"subtle skin pores"` in the realism modifier pool**
  - File: `AI.ProfilePhotoMaker.API/Services/ImageProcessing/ReplicateApiClient.cs`
  - Action: In `s_realismPromptModifiers` (lines 24–32), change `"subtle skin pores"` to `"soft natural finish"`.
  - Notes: This is a one-line string change in a `private static readonly string[]`. No other code in this array needs touching.

- [ ] **Task 2: Append universal blemish negatives in `CreateFluxNegativePrompt` (real client)**
  - File: `AI.ProfilePhotoMaker.API/Services/ImageProcessing/ReplicateApiClient.cs`
  - Action: In `CreateFluxNegativePrompt` (~line 961), after the `.Replace("  ", " ").Trim()` line and before the final `return`, add:
    ```csharp
    if (!string.IsNullOrWhiteSpace(result))
        result += ", moles, dark spots, skin blemishes, birthmarks, skin spots";
    ```
  - Notes: The early-return guard `if (string.IsNullOrWhiteSpace(negativePromptTemplate)) return string.Empty;` already handles the empty case — the new block only executes for non-empty results.

- [ ] **Task 3: Mirror same change in `MockReplicateApiClient`**
  - File: `AI.ProfilePhotoMaker.API/Services/ImageProcessing/MockReplicateApiClient.cs`
  - Action: In `CreateFluxNegativePrompt` (~line 342), apply the identical block as Task 2 — after `.Replace("  ", " ").Trim()`, before `return`:
    ```csharp
    if (!string.IsNullOrWhiteSpace(result))
        result += ", moles, dark spots, skin blemishes, birthmarks, skin spots";
    ```
  - Notes: This is a verbatim duplicate of the real client method. Change must be identical.

- [ ] **Task 4: Create new EF Core migration**
  - File: New `AI.ProfilePhotoMaker.API/Migrations/20260218xxxxxx_FixSkinBlemishAndWaxyForehead.cs`
  - Action:
    1. From `AI.ProfilePhotoMaker.API/` project directory run: `dotnet ef migrations add FixSkinBlemishAndWaxyForehead`
    2. Open the generated `.cs` file and replace the empty `Up()` and `Down()` bodies with the SQL below.
    3. Do **not** touch the `.Designer.cs` file.
  - **⚠️ CRITICAL FIRST STEP — verify the `@old` string:** Before writing the migration, open `AI.ProfilePhotoMaker.API/Migrations/20260111095443_SoftenSkinRealismConstraints.cs` and copy the exact `SkinRealismNegativePrompt` constant value verbatim. Do NOT retype it. A single character difference causes `REPLACE()` to silently no-op with no error.
  - `Up()` SQL:
    ```sql
    DECLARE @old nvarchar(max) = 'waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity';
    DECLARE @new nvarchar(max) = 'waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, oily forehead, shiny forehead, specular highlights on skin, skin gleam, HDR, oversharpened, too much clarity, moles, dark spots, skin blemishes, birthmarks, skin spots';

    UPDATE dbo.Styles
    SET NegativePromptTemplate = REPLACE(NegativePromptTemplate, @old, @new),
        UpdatedAt = GETUTCDATE()
    WHERE IsActive = 1
      AND NegativePromptTemplate LIKE '%waxy skin%';

    -- Sanity check: if 0 rows updated, the @old string did not match. Raise an error.
    IF @@ROWCOUNT = 0
        RAISERROR('FixSkinBlemishAndWaxyForehead Up(): No rows updated — @old string does not match any active style. Verify against SoftenSkinRealismConstraints.cs.', 16, 1);
    ```
  - `Down()` SQL (reverses exactly):
    ```sql
    DECLARE @new nvarchar(max) = 'waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, oily forehead, shiny forehead, specular highlights on skin, skin gleam, HDR, oversharpened, too much clarity, moles, dark spots, skin blemishes, birthmarks, skin spots';
    DECLARE @old nvarchar(max) = 'waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity';

    UPDATE dbo.Styles
    SET NegativePromptTemplate = REPLACE(NegativePromptTemplate, @new, @old),
        UpdatedAt = GETUTCDATE()
    WHERE IsActive = 1
      AND NegativePromptTemplate LIKE '%waxy skin%';
    ```
  - **`retro-wave` / `night-out` bespoke templates:** These styles have non-standard skin negative language. After running `Up()`, verify them manually: `SELECT Name, NegativePromptTemplate FROM dbo.Styles WHERE Name IN ('retro-wave', 'night-out')`. If their templates do NOT contain `@old` verbatim, they will be missed by `REPLACE()` — update them with a separate targeted SQL statement in the same migration.
  - **Snapshot note:** Since this is a data-only migration (no model changes), EF may generate an empty `Up()`/`Down()` scaffold AND may not touch `ApplicationDbContextModelSnapshot.cs`. Verify the snapshot is unchanged after `dotnet ef migrations add`. If EF modifies it unexpectedly, review the diff and revert any snapshot-only noise before committing.

- [ ] **Task 5: Fix breaking test — `ReplicateApiClientStyleTuningTests`**
  - File: `AI.ProfilePhotoMaker.API.Tests/Unit/ReplicateApiClientStyleTuningTests.cs`
  - Action: In `GenerateImagesAsync_AppendsRealismModifier` (~line 163), in the `allowedModifiers` string array, replace `"subtle skin pores"` with `"soft natural finish"`.
  - Notes: The test asserts that one of the allowed modifiers appears in the prompt. After Task 1, `"subtle skin pores"` is no longer in the pool, so this test would fail. `"soft natural finish"` is its replacement.

- [ ] **Task 6: Fix breaking test — `ReplicateApiClientNegativePromptTests`**
  - File: `AI.ProfilePhotoMaker.API.Tests/Unit/ReplicateApiClientNegativePromptTests.cs`
  - Action: In `GenerateImagesAsync_IncludesNegativePromptFromStyleTemplate` (~line 97), update the assertion:
    ```csharp
    // Before:
    Assert.Equal("formal business attire, suit, tie", negativePrompt.GetString());

    // After:
    Assert.Equal("formal business attire, suit, tie, moles, dark spots, skin blemishes, birthmarks, skin spots", negativePrompt.GetString());
    ```
  - Notes: The style seeded in this test has `NegativePromptTemplate = "formal business attire, suit, tie"`. After Tasks 2 & 3, the blemish suffix is always appended to non-empty templates.

- [ ] **Task 7: Add new unit tests for blemish suffix behaviour**
  - File: `AI.ProfilePhotoMaker.API.Tests/Unit/ReplicateApiClientNegativePromptTests.cs`
  - Action: Add two new `[Fact]` tests to the `ReplicateApiClientNegativePromptTests` class:

  **Test A — `ApplyRealismModifier_NeverProducesSubtleSkinPores`:**
  ```csharp
  [Fact]
  public void ApplyRealismModifier_NeverProducesSubtleSkinPores()
  {
      var basePrompt = "professional portrait of a person";
      bool foundSoftNaturalFinish = false;

      for (int i = 0; i < 100; i++)
      {
          var result = ReplicateApiClient.ApplyRealismModifier(basePrompt, new Random(i));
          Assert.DoesNotContain("subtle skin pores", result, StringComparison.OrdinalIgnoreCase);
          if (result.Contains("soft natural finish", StringComparison.OrdinalIgnoreCase))
              foundSoftNaturalFinish = true;
      }

      Assert.True(foundSoftNaturalFinish, "Expected 'soft natural finish' to appear in at least one iteration");
  }
  ```

  **Test B — `GenerateImagesAsync_AppendsBlemishNegatives_ToNonEmptyTemplate`** (complete implementation):
  ```csharp
  [Fact]
  public async Task GenerateImagesAsync_AppendsBlemishNegatives_ToNonEmptyTemplate()
  {
      // Arrange
      HttpRequestMessage? capturedRequest = null;
      var responseJson = JsonSerializer.Serialize(new
      {
          id = "pred-blemish",
          version = "test-version",
          status = "starting",
          created_at = DateTime.UtcNow
      });

      var httpHandlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
      httpHandlerMock
          .Protected()
          .Setup<Task<HttpResponseMessage>>(
              "SendAsync",
              ItExpr.Is<HttpRequestMessage>(req =>
                  req.Method == HttpMethod.Post &&
                  req.RequestUri != null &&
                  req.RequestUri.AbsolutePath.EndsWith("/predictions")),
              ItExpr.IsAny<CancellationToken>())
          .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
          .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
          {
              Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
          });

      var configMock = new Mock<IConfiguration>();
      configMock.Setup(x => x["Replicate:ApiToken"]).Returns("test-token");

      var webhookResolver = new Mock<IWebhookUrlResolver>();
      webhookResolver
          .Setup(x => x.GetWebhookUrlAsync(It.IsAny<string>()))
          .ReturnsAsync("https://example.com/webhook");

      var loggerMock = new Mock<ILogger<ReplicateApiClient>>();

      var options = new DbContextOptionsBuilder<ApplicationDbContext>()
          .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
          .Options;
      await using var db = new ApplicationDbContext(options);
      db.Styles.Add(new Style
      {
          Id = 998,
          Name = "corporate",
          Description = "Corporate style",
          PromptTemplate = "A photo of {subject}, corporate portrait",
          NegativePromptTemplate = "casual clothing, unprofessional",
          IsActive = true,
          CreatedAt = DateTime.UtcNow,
          UpdatedAt = DateTime.UtcNow
      });
      await db.SaveChangesAsync();

      var client = new ReplicateApiClient(
          new HttpClient(httpHandlerMock.Object),
          configMock.Object,
          loggerMock.Object,
          db,
          webhookResolver.Object);

      // Act
      await client.GenerateImagesAsync(
          trainedModelVersion: "owner/model:version",
          userId: "u-blemish",
          style: "corporate",
          userInfo: new UserInfo { Gender = "male", Ethnicity = "asian" },
          numOutputs: 2);

      // Assert
      Assert.NotNull(capturedRequest);
      var body = await capturedRequest!.Content!.ReadAsStringAsync();
      using var document = JsonDocument.Parse(body);
      var negativePrompt = document.RootElement.GetProperty("input").GetProperty("negative_prompt").GetString();
      Assert.Contains("moles, dark spots, skin blemishes, birthmarks, skin spots",
          negativePrompt, StringComparison.OrdinalIgnoreCase);
  }
  ```

- [ ] **Task 8: Add AC3 edge-case test — empty template produces no dangling suffix**
  - File: `AI.ProfilePhotoMaker.API.Tests/Unit/ReplicateApiClientNegativePromptTests.cs`
  - Action: Add a third new `[Fact]` test to cover AC3:
  ```csharp
  [Fact]
  public async Task GenerateImagesAsync_EmptyNegativeTemplate_ProducesEmptyNegativePrompt()
  {
      // Arrange — same boilerplate as Test B above, but seed a style with empty NegativePromptTemplate
      // Style seeded: NegativePromptTemplate = ""  (empty string)
      // Act: call GenerateImagesAsync for that style
      // Assert: input.negative_prompt is empty string
      Assert.Equal(string.Empty, negativePrompt);
  }
  ```
  - Notes: Follow the exact same Arrange boilerplate as Test B. Seed the style with `NegativePromptTemplate = string.Empty`. The full `Assert` target is `document.RootElement.GetProperty("input").GetProperty("negative_prompt").GetString()` which should equal `""`.
  - **Why:** AC3 has no automated test coverage without this task. This ensures the `if (!string.IsNullOrWhiteSpace(result))` guard in `CreateFluxNegativePrompt` is verified to not produce a dangling `, moles, ...` suffix on empty templates.

### Acceptance Criteria

- [ ] **AC1 — Blemish modifier removed from pool:**
  - Given: `ApplyRealismModifier` is called 100 times with different seeded `Random` instances
  - When: The output prompts are inspected
  - Then: No output ever contains `"subtle skin pores"`
  - And: At least one output contains `"soft natural finish"`

- [ ] **AC2 — Blemish suffix appended universally (non-empty template):**
  - Given: `GenerateImagesAsync` OR `GenerateBaseStylePreviewAsync` is called for any style with a non-empty `NegativePromptTemplate`
  - When: The Replicate API request body is inspected
  - Then: `input.negative_prompt` ends with `, moles, dark spots, skin blemishes, birthmarks, skin spots`
  - Note: Both entry points call `CreateFluxNegativePrompt` — the fix in Tasks 2 & 3 covers both paths automatically.

- [ ] **AC3 — Empty template edge case produces no dangling suffix:**
  - Given: A style exists with an empty or whitespace `NegativePromptTemplate`
  - When: `GenerateImagesAsync` is called for that style
  - Then: `input.negative_prompt` is empty string (not `, moles, dark spots...` alone)
  - Covered by: Task 8 unit test

- [ ] **AC4 — Migration Up strengthens waxy/forehead language in DB:**
  - Given: Migration `FixSkinBlemishAndWaxyForehead` `Up()` has been applied
  - When: `dbo.Styles` is queried for active rows where `NegativePromptTemplate LIKE '%waxy skin%'`
  - Then: Every such row's `NegativePromptTemplate` contains `oily forehead, shiny forehead, specular highlights on skin, skin gleam`
  - And: Every such row's `NegativePromptTemplate` contains `moles, dark spots, skin blemishes, birthmarks`
  - And: No active row contains the old skin segment verbatim (i.e. `...harsh facial shadows, HDR...` without the new forehead terms between them)

- [ ] **AC5 — Migration is fully reversible:**
  - Given: Migration `FixSkinBlemishAndWaxyForehead` `Up()` has been applied, then `Down()` applied
  - When: `dbo.Styles` is queried
  - Then: Active style rows revert to the `SoftenSkinRealismConstraints` skin segment (no forehead or blemish terms remain)

- [ ] **AC6 — All existing tests pass:**
  - Given: Tasks 5 & 6 have been applied (test fixes)
  - When: `dotnet test` is run for the `AI.ProfilePhotoMaker.API.Tests` project
  - Then: All tests pass with 0 failures

---

## Additional Context

### Dependencies

- **EF Core CLI tooling** (`dotnet ef`) must be installed locally. Run `dotnet ef migrations add FixSkinBlemishAndWaxyForehead` from the `AI.ProfilePhotoMaker.API/` directory before filling in the migration body.
- No new NuGet packages.
- No frontend changes.
- No infrastructure changes (migration auto-runs on startup via `AutoMigrateOnStartup: true` in `appsettings.json`).

### Testing Strategy

**Unit tests (automated):**
- Fix `ReplicateApiClientStyleTuningTests.GenerateImagesAsync_AppendsRealismModifier` — update hardcoded modifier list (Task 5)
- Fix `ReplicateApiClientNegativePromptTests.GenerateImagesAsync_IncludesNegativePromptFromStyleTemplate` — update exact assertion (Task 6)
- Add `ApplyRealismModifier_NeverProducesSubtleSkinPores` — seeded random loop, 100 iterations (Task 7A)
- Add `GenerateImagesAsync_AppendsBlemishNegatives_ToNonEmptyTemplate` — HTTP mock pattern (Task 7B)
- Run `dotnet test` to verify 0 failures before submitting

**Manual verification (post-deploy):**
- Generate a photo on each of the 6 professional styles (Academic, LinkedIn, Executive, Startup, Tech-Professional, Entrepreneur)
- Inspect DB: `SELECT Name, NegativePromptTemplate FROM dbo.Styles WHERE IsActive = 1 AND NegativePromptTemplate LIKE '%waxy skin%'` — confirm new forehead and blemish terms present in all rows

### Notes

- **`SoftenSkinRealismConstraints` history:** That migration deliberately removed `poreless skin` and `exaggerated wrinkles` because they caused dry/aged-looking skin. The new migration must NOT re-introduce either of those terms.
- **Styles excluded from migration:** `corporate`, `consultant`, `influencer`, `digital-nomad`, `fitness`, `glamour`, `digital-native` all lack `"waxy skin"` in their negative prompt — they won't be touched. This is consistent with all prior skin migrations.
- **`retro-wave` and `night-out` bespoke templates:** These styles have custom skin language that does NOT match `@old` verbatim, even though they'll be caught by the `LIKE '%waxy skin%'` WHERE filter. The `REPLACE(@old, @new)` will silently no-op for these rows. **Action required in Task 4:** after the main `UPDATE`, add targeted SQL for these styles — either verify their exact current template and use a specific `REPLACE()`, or update them with explicit `SET NegativePromptTemplate = ... WHERE Name IN ('retro-wave', 'night-out')`. Check their values first: `SELECT Name, NegativePromptTemplate FROM dbo.Styles WHERE Name IN ('retro-wave', 'night-out')`.
- **`@@ROWCOUNT` guard:** The `Up()` SQL includes a `RAISERROR` if zero rows are updated. This is intentional — it ensures the migration fails loudly rather than silently succeeding with no effect if the `@old` string doesn't match.
- **`skin spots` consistency:** The runtime suffix (code) and the DB migration `@new` string both include `skin spots` as of this spec. Ensure they stay in sync if either is modified.
- **`AutoMigrateOnStartup` deployment:** The migration runs automatically on app startup. Post-deploy, run the verification query to confirm rows were updated: `SELECT Name, NegativePromptTemplate FROM dbo.Styles WHERE IsActive = 1 AND NegativePromptTemplate LIKE '%oily forehead%'` — should return all professional cluster styles.
- **Future cleanup:** Extract `CreateFluxNegativePrompt` into a shared static utility to eliminate the `ReplicateApiClient` / `MockReplicateApiClient` duplication. Not in scope here.

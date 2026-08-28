import { test, expect, Page, Route } from "@playwright/test";
import * as path from "node:path";
import { mkdirSync } from "node:fs";

const reviewDir = path.resolve(__dirname, "../../../../.impeccable/review");
const sourcePhotoPath = path.resolve(
  __dirname,
  "../../../../AI.ProfilePhotoMaker.UI/src/assets/marketing/before-after/linkedin-before.jpeg",
);
const stylePhotoPath = path.resolve(
  __dirname,
  "../../../../AI.ProfilePhotoMaker.UI/src/assets/marketing/before-after/linkedin-after.jpg",
);
mkdirSync(reviewDir, { recursive: true });

const baseOrigin = process.env.BASE_URL || "http://localhost:4200";
const jwtPayload = Buffer.from(
  JSON.stringify({
    sub: "user-1",
    email: "user@example.com",
    exp: Math.floor(Date.now() / 1000) + 3600,
  }),
).toString("base64");
const mockToken = `mock.${jwtPayload}.signature`;

const score = {
  overallScore: 82,
  ratingLabel: "Profile-ready",
  subscores: [
    {
      code: "face_presence",
      label: "Face presence",
      score: 84,
      feedback: "Face visible.",
    },
    {
      code: "lighting",
      label: "Lighting",
      score: 80,
      feedback: "Lighting OK.",
    },
    {
      code: "background",
      label: "Background",
      score: 78,
      feedback: "Usable background.",
    },
    {
      code: "platform_fit",
      label: "Platform fit",
      score: 86,
      feedback: "Good platform crop.",
    },
  ],
  strengths: ["Clear enough for review"],
  improvements: ["Could refine background"],
  guidance: "Ready to test the profile photo workflow.",
  qualityGate: { status: "pass", reasons: [], recommendations: [] },
};

test.use({
  storageState: {
    cookies: [],
    origins: [
      ...["http://127.0.0.1:4300", "http://localhost:4300", baseOrigin].map(
        (origin) => ({
          origin,
          localStorage: [
            { name: "auth_token", value: mockToken },
            {
              name: "currentUser",
              value: JSON.stringify({
                token: mockToken,
                email: "user@example.com",
                firstName: "Test",
                lastName: "User",
              }),
            },
            {
              name: "biometricConsent",
              value: JSON.stringify({
                accepted: true,
                acceptedAt: new Date().toISOString(),
              }),
            },
            { name: "e2eAuthBypass", value: "true" },
          ],
        }),
      ),
    ],
  },
});

async function fulfillJson(route: Route, data: unknown) {
  await route.fulfill({
    status: 200,
    contentType: "application/json",
    body: JSON.stringify(data),
  });
}

async function installCommonRoutes(
  page: Page,
  features: Record<string, boolean>,
) {
  await page.route("**/api/config/client", (route) =>
    fulfillJson(route, {
      success: true,
      data: {
        appBaseUrl: "http://localhost:5032",
        apiBaseUrl: "http://localhost:5032/api",
        environment: "development",
        isDevelopment: true,
        isProduction: false,
        features,
      },
    }),
  );

  await page.route("**/api/auth/**", (route) => {
    const url = route.request().url();
    const body = url.includes("profile-completion-status")
      ? { isCompleted: true }
      : {
          success: true,
          data: { valid: true, emailConfirmed: true },
          error: null,
        };
    return fulfillJson(route, body);
  });

  await page.route("**/api/credit/status", (route) =>
    fulfillJson(route, {
      success: true,
      data: {
        credits: 5,
        lastCreditReset: new Date().toISOString(),
        nextResetDate: new Date().toISOString(),
      },
      error: null,
    }),
  );

  await page.route("**/profile-images/style-previews/**", (route) =>
    route.fulfill({
      status: 200,
      contentType: "image/jpeg",
      path: stylePhotoPath,
    }),
  );

  await page.route("**/api/style-preview/list", (route) =>
    fulfillJson(route, { success: true, count: 0, previews: [] }),
  );

  await page.route("**/api/style", (route) =>
    fulfillJson(route, {
      success: true,
      data: [
        {
          id: 1,
          name: "LinkedIn",
          description: "Clean professional portrait",
          promptTemplate: "Professional portrait",
          negativePromptTemplate: "",
          isActive: true,
          createdAt: new Date().toISOString(),
          updatedAt: new Date().toISOString(),
        },
      ],
      error: null,
    }),
  );

  await page.route("**/api/profilephotoworkflow/packages", (route) =>
    fulfillJson(route, {
      success: true,
      data: [
        {
          id: 1,
          code: "free_preview",
          name: "Free Preview",
          description: "Preview",
          price: 0,
          currency: "USD",
          includedCandidateCount: 1,
          includedRefinementCount: 0,
          includedPremiumAugmentationCount: 0,
          includesPlatformExportKit: false,
          includesScoreDelta: true,
          displayOrder: 1,
          highlights: [],
        },
        {
          id: 2,
          code: "starter_package",
          name: "Starter Package",
          description: "Starter",
          price: 900,
          currency: "USD",
          internalCreditPackageId: 2,
          includedCandidateCount: 3,
          includedRefinementCount: 1,
          includedPremiumAugmentationCount: 0,
          includesPlatformExportKit: true,
          includesScoreDelta: true,
          displayOrder: 2,
          highlights: [],
        },
        {
          id: 3,
          code: "pro_package",
          name: "Pro Package",
          description: "Complete professional set",
          price: 1900,
          currency: "USD",
          internalCreditPackageId: 3,
          includedCandidateCount: 9,
          includedRefinementCount: 3,
          includedPremiumAugmentationCount: 3,
          includesPlatformExportKit: true,
          includesScoreDelta: true,
          displayOrder: 3,
          highlights: [],
        },
      ],
      error: null,
    }),
  );

  await page.route("**/api/profilephotoworkflow/entitlements", (route) =>
    fulfillJson(route, {
      success: true,
      data: [
        {
          id: 7,
          packageCode: "starter_package",
          packageName: "Starter Package",
          status: "active",
          remainingPackageUses: 1,
          remainingCandidates: 3,
          remainingRefinements: 1,
          remainingPremiumAugmentations: 0,
          platformExportKitAvailable: true,
        },
      ],
      error: null,
    }),
  );

  await page.route("**/api/profilephotoworkflow/export-options", (route) =>
    fulfillJson(route, {
      success: true,
      data: [
        {
          code: "linkedin_profile",
          label: "LinkedIn profile",
          width: 800,
          height: 800,
          fileNameSuffix: "linkedin",
        },
      ],
      error: null,
    }),
  );

  await page.route("**/api/profilephotoworkflow/score", (route) =>
    fulfillJson(route, { success: true, data: score, error: null }),
  );
  await page.route("**/api/profilephotoworkflow/score-image/**", (route) =>
    fulfillJson(route, { success: true, data: score, error: null }),
  );

  const emptyImageState = {
    success: true,
    data: { images: [], generatedImages: 9, totalImages: 9 },
    error: null,
  };
  await page.route("**/api/image/user-images**", (route) =>
    fulfillJson(route, emptyImageState),
  );
  await page.route("**/api/image/images**", (route) =>
    fulfillJson(route, emptyImageState),
  );

  await page.route("**/api/image/upload**", (route) =>
    fulfillJson(route, {
      success: true,
      data: {
        uploadedFiles: [
          {
            id: "source-1",
            url: "https://cdn.example.test/source.png",
            storagePath: "dev/enhanced/user-1/source.png",
            fileName: "source.png",
          },
        ],
      },
      error: null,
    }),
  );
}

async function expectTouchTargetsAtLeast44px(page: Page) {
  const undersized = await page.evaluate(() =>
    Array.from(
      document.querySelectorAll<HTMLElement>(
        "button, select, input[type='range'], label.consent-checkbox, label.fulfillment-consent, label.retention-item, header a, main a",
      ),
    )
      .filter((element) => {
        const style = getComputedStyle(element);
        const rect = element.getBoundingClientRect();
        return (
          style.display !== "none" &&
          style.visibility !== "hidden" &&
          style.pointerEvents !== "none" &&
          rect.width > 0 &&
          rect.height > 0
        );
      })
      .map((element) => {
        const rect = element.getBoundingClientRect();
        return {
          label:
            element.getAttribute("aria-label") ||
            element.textContent?.trim().replace(/\s+/g, " ").slice(0, 60) ||
            element.tagName,
          width: Math.round(rect.width),
          height: Math.round(rect.height),
        };
      })
      .filter((target) => target.width < 44 || target.height < 44),
  );
  expect(undersized).toEqual([]);
}

async function expect200PercentZoomReflow(page: Page) {
  // Browser zoom halves the CSS viewport. A 720px device at 200% therefore reflows at 360 CSS px.
  await page.setViewportSize({ width: 720 / 2, height: 900 });
  const layout = await page.evaluate(() => ({
    viewport: window.innerWidth,
    scrollWidth: document.documentElement.scrollWidth,
  }));
  expect(layout.viewport).toBe(360);
  expect(layout.scrollWidth).toBeLessThanOrEqual(layout.viewport + 1);
  await expectTouchTargetsAtLeast44px(page);
}

async function openWorkspaceWithPhoto(page: Page) {
  await page.goto("/app/enhance?e2eAuthBypass=1");
  await page
    .getByRole("button", { name: /Accept All/i })
    .click()
    .catch(() => undefined);
  await expect(
    page.getByRole("heading", {
      name: /Your professional photo, proofed and ready|Photo Workspace/i,
    }),
  ).toBeVisible({ timeout: 20_000 });

  const fileChooserPromise = page.waitForEvent("filechooser");
  await page
    .getByRole("button", {
      name: /Choose your source photo|Upload a photo to transform/i,
    })
    .click();
  const fileChooser = await fileChooserPromise;
  await fileChooser.setFiles(sourcePhotoPath);
}

test.describe("Profile workflow flags, UX, and downloads", () => {
  test("free preview generated result is available as a browser download", async ({
    page,
  }) => {
    await installCommonRoutes(page, {
      openAIHeadshotMvp: true,
      profilePhotoWorkflowOverhaul: true,
      outcomePackagesVisible: true,
      profilePhotoScoreVisible: true,
      creativeStylePackVisible: true,
      premiumAugmentationsVisible: true,
      replicateTrainingFlowVisible: false,
    });

    await page.route("**/api/profilephotoworkflow/entitlements", (route) =>
      fulfillJson(route, { success: true, data: [], error: null }),
    );

    let headshotCalled = false;
    await page.route("**/api/headshots/generate", (route) => {
      headshotCalled = true;
      const request = route.request().postDataJSON();
      expect(request.packageCode).toBe("free_preview");
      expect(request.numOutputs).toBe(1);
      return fulfillJson(route, {
        success: true,
        data: {
          success: true,
          imageUrl: "data:image/png;base64,iVBORw0KGgo=",
          storagePath: "dev/generated/user-1/free-preview.png",
          processedImageId: 101,
          provider: "openai",
          model: "gpt-image-2",
          style: "general_professional",
          background: "auto",
          creditsCost: 0,
          remainingCredits: 5,
          correlationId: "free-preview",
        },
        error: null,
      });
    });

    await openWorkspaceWithPhoto(page);
    await expect(page.getByText(/82\/100/).first()).toBeVisible({
      timeout: 20_000,
    });
    await expect(
      page.getByText("Free Preview does not include the platform export kit"),
    ).toHaveCount(0);
    await page
      .getByRole("checkbox", { name: /biometric data/i })
      .check({ force: true });
    await page.setViewportSize({ width: 1280, height: 900 });
    await expectTouchTargetsAtLeast44px(page);
    await page.screenshot({
      path: path.join(reviewDir, "pre-generation-desktop.png"),
      fullPage: true,
      animations: "disabled",
    });
    await page.setViewportSize({ width: 390, height: 844 });
    await expectTouchTargetsAtLeast44px(page);
    await page.screenshot({
      path: path.join(reviewDir, "pre-generation-mobile.png"),
      fullPage: true,
      animations: "disabled",
    });
    await page.setViewportSize({ width: 1280, height: 900 });
    await page
      .getByRole("button", {
        name: /Generate Free Preview|Generate Candidate|Transform Photo/i,
      })
      .click();

    await expect(
      page.getByRole("heading", { name: "Your candidate set is ready" }),
    ).toBeVisible({ timeout: 10_000 });
    await expect(
      page.getByText(/Free Preview includes a watermarked download/),
    ).toBeVisible();
    await expect(
      page.getByRole("button", { name: /Download Watermarked Preview/i }),
    ).toBeVisible();
    await expect(page.getByText("Not included")).toHaveCount(2);
    await expect(page.getByText("Watermarked only")).toBeVisible();
    const freeReviewPrimaryActions = page.locator(
      ".results-section .btn-primary:visible",
    );
    await expect(freeReviewPrimaryActions).toHaveCount(1);
    await expect(freeReviewPrimaryActions).toHaveAccessibleName(
      "Upgrade to Starter Package",
    );
    expect(headshotCalled).toBeTruthy();
  });

  test("Free Preview upgrades to Starter and restores the completed set after reload", async ({
    page,
  }) => {
    await installCommonRoutes(page, {
      openAIHeadshotMvp: true,
      profilePhotoWorkflowOverhaul: true,
      outcomePackagesVisible: true,
      profilePhotoScoreVisible: true,
      creativeStylePackVisible: true,
      premiumAugmentationsVisible: true,
      replicateTrainingFlowVisible: false,
    });

    let packageStage: "free" | "starter" | "complete" = "free";
    await page.route("**/api/profilephotoworkflow/entitlements", (route) =>
      fulfillJson(route, {
        success: true,
        data:
          packageStage === "free"
            ? []
            : [
                {
                  id: 12,
                  packageCode: "starter_package",
                  packageName: "Starter Package",
                  status: "active",
                  remainingPackageUses: packageStage === "starter" ? 1 : 0,
                  remainingCandidates: packageStage === "starter" ? 2 : 0,
                  remainingRefinements: 1,
                  remainingPremiumAugmentations: 0,
                  platformExportKitAvailable: true,
                },
              ],
        error: null,
      }),
    );

    const promotedCandidate = {
      imageUrl: `${baseOrigin}/assets/marketing/before-after/linkedin-after.jpg`,
      storagePath: "",
      processedImageId: 101,
      provider: "openai",
      model: "gpt-image-2",
      correlationId: "purchase:12:promoted-preview",
    };
    const starterCandidates = [
      promotedCandidate,
      {
        ...promotedCandidate,
        imageUrl: `${baseOrigin}/assets/marketing/before-after/executive-after.jpg`,
        processedImageId: 102,
        correlationId: "starter:candidate:1",
      },
      {
        ...promotedCandidate,
        processedImageId: 103,
        correlationId: "starter:candidate:2",
      },
    ];

    await page.route("**/api/headshots/resumable-preview**", (route) =>
      fulfillJson(route, {
        success: true,
        data:
          packageStage === "free"
            ? null
            : {
                processedImageId: 101,
                imageUrl: promotedCandidate.imageUrl,
                storagePath: "",
                sourceStoragePath: "dev/uploads/user-1/source.png",
                style: "LinkedIn",
                createdAt: new Date().toISOString(),
                hasRawPreview: true,
                canPromotePreview: false,
                activePackageCode: "starter_package",
                remainingCandidateCount: packageStage === "starter" ? 2 : 0,
                promotedCandidate,
                candidates:
                  packageStage === "complete"
                    ? starterCandidates
                    : [promotedCandidate],
                message:
                  packageStage === "complete"
                    ? "Your Starter candidate set is ready to review."
                    : "Your preview is unlocked. Generate the remaining paid candidates when ready.",
              },
        error: null,
      }),
    );

    const generationRequests: Array<{
      packageCode: string;
      numOutputs: number;
      isRegeneration?: boolean;
    }> = [];
    await page.route("**/api/headshots/generate", (route) => {
      const request = route.request().postDataJSON();
      generationRequests.push(request);
      if (request.packageCode === "free_preview") {
        return fulfillJson(route, {
          success: true,
          data: {
            success: true,
            ...promotedCandidate,
            style: "LinkedIn",
            background: "auto",
            creditsCost: 0,
            remainingCredits: 5,
            candidates: [promotedCandidate],
          },
          error: null,
        });
      }

      expect(request.packageCode).toBe("starter_package");
      expect(request.numOutputs).toBe(2);
      expect(request.isRegeneration).toBeFalsy();
      packageStage = "complete";
      return fulfillJson(route, {
        success: true,
        data: {
          success: true,
          ...starterCandidates[0],
          style: "LinkedIn",
          background: "auto",
          creditsCost: 2,
          remainingCredits: 3,
          candidates: starterCandidates,
        },
        error: null,
      });
    });

    await openWorkspaceWithPhoto(page);
    await expect(page.getByText(/82\/100/).first()).toBeVisible({
      timeout: 20_000,
    });
    await page
      .getByRole("checkbox", { name: /biometric data/i })
      .check({ force: true });
    await page.getByRole("button", { name: "Generate Free Preview" }).click();
    await expect(page.getByText("1 of 1 generated").first()).toBeVisible({
      timeout: 20_000,
    });
    await expect(page.getByText("Not included").first()).toBeVisible();
    await expect(page.getByText("Watermarked only")).toBeVisible();

    await page
      .getByRole("button", { name: "Upgrade to Starter Package" })
      .click();
    await expect(page).toHaveURL(/\/pricing\?.*outcomePackage=starter_package/);

    packageStage = "starter";
    await page.goto(
      "/app/enhance?e2eAuthBypass=1&upgraded=starter_package&previewId=101",
    );
    await expect(page.getByText("1 of 3 generated").first()).toBeVisible({
      timeout: 20_000,
    });
    const remainingAction = page
      .getByRole("button", { name: "Generate remaining 2 photos" })
      .last();
    if (
      await page
        .getByRole("checkbox", { name: /remaining package photos/i })
        .count()
    ) {
      await page
        .getByRole("checkbox", { name: /remaining package photos/i })
        .click();
    }
    await expect(remainingAction).toBeEnabled();
    await remainingAction.click();

    await expect(page.getByText("3 of 3 generated").first()).toBeVisible({
      timeout: 20_000,
    });
    expect(generationRequests).toEqual([
      expect.objectContaining({ packageCode: "free_preview", numOutputs: 1 }),
      expect.objectContaining({
        packageCode: "starter_package",
        numOutputs: 2,
      }),
    ]);

    await page.reload();
    await expect(page.getByText("3 of 3 generated").first()).toBeVisible({
      timeout: 20_000,
    });
    await expect(page.getByRole("button", { name: /Candidate /i })).toHaveCount(
      3,
    );
    await expect(
      page.getByRole("button", { name: "Generate remaining 2 photos" }),
    ).toHaveCount(0);
    await expect(
      page.getByRole("button", { name: /Download Package/i }),
    ).toBeVisible();
  });

  test("interrupted generation resumes with the same idempotent request", async ({
    page,
  }) => {
    await installCommonRoutes(page, {
      openAIHeadshotMvp: true,
      profilePhotoWorkflowOverhaul: true,
      outcomePackagesVisible: true,
      profilePhotoScoreVisible: true,
      creativeStylePackVisible: true,
      premiumAugmentationsVisible: true,
      replicateTrainingFlowVisible: false,
    });
    await page.route("**/api/headshots/resumable-preview**", (route) =>
      fulfillJson(route, { success: true, data: null, error: null }),
    );

    let resumedRequest: Record<string, unknown> | null = null;
    await page.route("**/api/headshots/generate", (route) => {
      resumedRequest = route.request().postDataJSON();
      const candidates = Array.from({ length: 3 }, (_, index) => ({
        imageUrl: `${baseOrigin}/assets/marketing/before-after/${index % 2 ? "executive-after.jpg" : "linkedin-after.jpg"}`,
        storagePath: "",
        processedImageId: 300 + index,
        provider: "openai",
        model: "gpt-image-2",
        correlationId: `resumed-${index + 1}`,
      }));
      return fulfillJson(route, {
        success: true,
        data: {
          success: true,
          ...candidates[0],
          style: "LinkedIn",
          background: "auto",
          creditsCost: 3,
          remainingCredits: 2,
          candidates,
        },
        error: null,
      });
    });

    await page.addInitScript(() => {
      localStorage.setItem(
        "photoWorkspaceInterruptedGeneration",
        JSON.stringify({
          clientRequestId: "interrupted-request-123",
          imageStoragePath: "dev/uploads/user-1/interrupted-source.png",
          styleName: "LinkedIn",
          packageCode: "starter_package",
          useCaseCode: "linkedin_executive",
          isRegeneration: false,
          startedAt: new Date().toISOString(),
        }),
      );
    });

    await page.goto("/app/enhance?e2eAuthBypass=1");
    await page
      .getByRole("button", { name: /Accept All/i })
      .click()
      .catch(() => undefined);
    await expect(
      page.getByRole("heading", { name: "Resume your interrupted generation" }),
    ).toBeVisible({
      timeout: 20_000,
    });
    const recoveryConsent = page.getByRole("checkbox", {
      name: /saved request/i,
    });
    if (await recoveryConsent.count()) await recoveryConsent.click();
    const resumeButton = page.getByRole("button", {
      name: "Resume generation",
    });
    await expect(resumeButton).toBeEnabled({ timeout: 20_000 });
    await resumeButton.click();

    await expect(page.getByText("3 of 3 generated").first()).toBeVisible({
      timeout: 20_000,
    });
    expect(resumedRequest).toEqual(
      expect.objectContaining({
        clientRequestId: "interrupted-request-123",
        imageStoragePath: "dev/uploads/user-1/interrupted-source.png",
        packageCode: "starter_package",
        numOutputs: 3,
      }),
    );
    expect(
      await page.evaluate(() =>
        localStorage.getItem("photoWorkspaceInterruptedGeneration"),
      ),
    ).toBeNull();
  });

  test("Pro upgrade return makes remaining candidate fulfillment the primary action", async ({
    page,
  }) => {
    await page.addInitScript(() => localStorage.setItem("theme", "dark"));
    await installCommonRoutes(page, {
      openAIHeadshotMvp: true,
      profilePhotoWorkflowOverhaul: true,
      outcomePackagesVisible: true,
      profilePhotoScoreVisible: true,
      creativeStylePackVisible: true,
      premiumAugmentationsVisible: true,
      replicateTrainingFlowVisible: false,
    });

    let candidateAllowanceAvailable = false;
    let paidGenerationComplete = false;
    let restoredCandidateCount = 1;
    let refinementRequested = false;
    let refinementPersisted = false;
    let exportAvailable = true;
    await page.route("**/api/profilephotoworkflow/entitlements", (route) =>
      fulfillJson(route, {
        success: true,
        data: [
          {
            id: 9,
            packageCode: "pro_package",
            packageName: "Pro Package",
            status: "active",
            remainingPackageUses: paidGenerationComplete ? 0 : 1,
            remainingCandidates: paidGenerationComplete
              ? 0
              : candidateAllowanceAvailable
                ? 9 - restoredCandidateCount
                : 0,
            remainingRefinements: 3,
            remainingPremiumAugmentations: 3,
            platformExportKitAvailable: exportAvailable,
          },
        ],
        error: null,
      }),
    );

    await page.route("**/profile-images/**", (route) =>
      route.fulfill({
        status: 200,
        contentType: "image/jpeg",
        path: sourcePhotoPath,
      }),
    );

    const proCandidates = Array.from({ length: 9 }, (_, index) => ({
      imageUrl:
        index === 0
          ? "/api/headshots/images/111/original"
          : `${baseOrigin}/assets/marketing/before-after/${index % 2 === 0 ? "linkedin-after.jpg" : "executive-after.jpg"}`,
      storagePath: `dev/generated/user-1/pro-${index + 1}.jpg`,
      processedImageId: 111 + index,
      provider: "openai",
      model: "gpt-image-2",
      correlationId: `pro-${index + 1}`,
    }));
    const refinementCandidate = {
      ...proCandidates[0],
      imageUrl: `${baseOrigin}/assets/marketing/before-after/set-1-after.jpg`,
      storagePath: "dev/generated/user-1/refinement-999.jpg",
      processedImageId: 999,
      correlationId: "pro-refinement-1",
    };
    await page.route("**/api/headshots/resumable-preview**", (route) =>
      fulfillJson(route, {
        success: true,
        data: {
          processedImageId: 101,
          imageUrl: proCandidates[0].imageUrl,
          storagePath: "",
          sourceStoragePath: "dev/uploads/user-1/source.png",
          style: "LinkedIn",
          createdAt: new Date().toISOString(),
          hasRawPreview: true,
          canPromotePreview: false,
          activePackageCode: "pro_package",
          remainingCandidateCount: paidGenerationComplete
            ? 0
            : 9 - restoredCandidateCount,
          promotedCandidate: proCandidates[0],
          candidates: paidGenerationComplete
            ? refinementPersisted
              ? [refinementCandidate, ...proCandidates.slice(1)]
              : proCandidates
            : proCandidates.slice(0, restoredCandidateCount),
          message: paidGenerationComplete
            ? "Your Pro candidate set is ready to review."
            : "Continue your Pro Package.",
        },
        error: null,
      }),
    );

    let requestedOutputs = 0;
    await page.route("**/api/headshots/generate", (route) => {
      const request = route.request().postDataJSON();
      expect(request.packageCode).toBe("pro_package");
      if (request.isRegeneration) {
        refinementRequested = true;
        expect(request.numOutputs).toBe(1);
        expect(request.reusedPreviewProcessedImageId).toBeUndefined();
        expect(request.replacesProcessedImageId).toBe(111);
        refinementPersisted = true;
        return fulfillJson(route, {
          success: true,
          data: {
            success: true,
            ...refinementCandidate,
            style: "LinkedIn",
            background: "auto",
            creditsCost: 1,
            remainingCredits: 0,
            candidates: [refinementCandidate],
          },
          error: null,
        });
      }

      requestedOutputs = request.numOutputs;
      expect(request.numOutputs).toBe(9 - restoredCandidateCount);
      expect(request.isRegeneration).toBeFalsy();
      paidGenerationComplete = true;
      return fulfillJson(route, {
        success: true,
        data: {
          success: true,
          ...proCandidates[0],
          style: "LinkedIn",
          background: "auto",
          creditsCost: requestedOutputs,
          remainingCredits: 0,
          candidates: [
            proCandidates[0],
            ...proCandidates.slice(restoredCandidateCount),
          ],
        },
        error: null,
      });
    });

    await page.route("**/api/profilephotoworkflow/export-package", (route) => {
      exportAvailable = false;
      return route.fulfill({
        status: 200,
        contentType: "application/zip",
        body: Buffer.from("PK\u0003\u0004"),
      });
    });

    await page.goto(
      "/app/enhance?e2eAuthBypass=1&upgraded=pro_package&previewId=101",
    );
    await page
      .getByRole("button", { name: /Accept All/i })
      .click()
      .catch(() => undefined);

    await expect(page.getByText("1 of 9 generated").first()).toBeVisible({
      timeout: 20_000,
    });
    await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");
    await expect(page.locator(".candidate-proof img").first()).toHaveAttribute(
      "src",
      /\/profile-images\/dev\/generated\/user-1\/pro-1\.jpg$/,
    );
    await expect(page.locator(".proof-studio")).toHaveCSS(
      "color-scheme",
      "dark",
    );
    await expect(page.locator(".proof-studio")).toHaveCSS(
      "background-color",
      "rgb(13, 17, 23)",
    );
    const candidateBadge = page.locator(".candidate-proof > span").first();
    await expect(candidateBadge).toHaveText("1");
    await expect(candidateBadge).toHaveCSS(
      "background-color",
      "rgb(13, 17, 23)",
    );
    await expect(candidateBadge).toHaveCSS("color", "rgb(241, 245, 249)");
    let fulfillmentAction = page
      .getByRole("button", { name: "Generate remaining 8 photos" })
      .last();
    await expect(fulfillmentAction).toBeVisible();
    await expect(fulfillmentAction).toHaveText("Generate remaining 8 photos");
    await expect(fulfillmentAction).toHaveCSS(
      "background-color",
      /rgb\((255, 255, 255|49, 95, 196)\)/,
    );
    await expect(fulfillmentAction).toHaveCSS(
      "color",
      /rgb\((23, 63, 153|241, 245, 249|255, 255, 255)\)/,
    );
    await expect(fulfillmentAction).toBeDisabled();
    await expect(
      page.getByText(
        /candidate allowance does not match the unfinished package/i,
      ),
    ).toBeVisible();

    candidateAllowanceAvailable = true;
    await page.reload();
    await expect(page.getByText("1 of 9 generated").first()).toBeVisible({
      timeout: 20_000,
    });
    fulfillmentAction = page
      .getByRole("button", { name: "Generate remaining 8 photos" })
      .last();
    await page
      .getByRole("checkbox", { name: /remaining package photos/i })
      .click();
    await expect(fulfillmentAction).toBeEnabled();
    await fulfillmentAction.focus();
    await expect(fulfillmentAction).toBeFocused();
    await page.setViewportSize({ width: 1280, height: 900 });
    await page.screenshot({
      path: path.join(reviewDir, "paid-upgrade-return-desktop.png"),
      fullPage: true,
      animations: "disabled",
    });
    await page.setViewportSize({ width: 390, height: 844 });
    await page.screenshot({
      path: path.join(reviewDir, "paid-upgrade-return-mobile.png"),
      fullPage: true,
      animations: "disabled",
    });

    for (const width of [360, 390, 768, 1280]) {
      await page.setViewportSize({ width, height: 900 });
      await expect(fulfillmentAction).toBeVisible();
      await expectTouchTargetsAtLeast44px(page);
      const layout = await page.evaluate(() => ({
        viewport: window.innerWidth,
        scrollWidth: document.documentElement.scrollWidth,
      }));
      expect(layout.scrollWidth).toBeLessThanOrEqual(layout.viewport + 1);
      const actionBox = await fulfillmentAction.boundingBox();
      expect(actionBox?.height ?? 0).toBeGreaterThanOrEqual(44);
      expect(actionBox?.width ?? 0).toBeGreaterThanOrEqual(44);
    }
    await expect200PercentZoomReflow(page);

    restoredCandidateCount = 4;
    await page.reload();
    await expect(page.getByText("4 of 9 generated").first()).toBeVisible({
      timeout: 20_000,
    });
    fulfillmentAction = page
      .getByRole("button", { name: "Generate remaining 5 photos" })
      .last();
    await expect(fulfillmentAction).toBeEnabled();
    await expect(page.getByRole("button", { name: /Candidate /i })).toHaveCount(
      4,
    );
    await expect(page.getByRole("button", { name: /Relighting/i })).toHaveCount(
      0,
    );
    await expect(
      page.getByRole("button", { name: /Download Package/i }),
    ).toHaveCount(0);

    await fulfillmentAction.click();

    await expect(page.getByText("9 of 9 generated").first()).toBeVisible({
      timeout: 20_000,
    });
    await expect(
      page.getByRole("heading", { name: "Your candidate set is ready" }),
    ).toBeVisible();
    await expect(page.getByText("Load Images Failed")).toHaveCount(0);
    await expect(
      page.getByRole("button", { name: /Relighting/i }),
    ).toBeVisible();
    const downloadPackage = page.getByRole("button", {
      name: /Download Package/i,
    });
    await expect(downloadPackage).toBeVisible();
    await page.setViewportSize({ width: 1280, height: 900 });
    await expectTouchTargetsAtLeast44px(page);
    await page.screenshot({
      path: path.join(reviewDir, "candidate-review-desktop.png"),
      fullPage: true,
      animations: "disabled",
    });
    await page.setViewportSize({ width: 390, height: 844 });
    await expectTouchTargetsAtLeast44px(page);
    await page.screenshot({
      path: path.join(reviewDir, "candidate-review-mobile.png"),
      fullPage: true,
      animations: "disabled",
    });
    await page.setViewportSize({ width: 1280, height: 900 });
    await page.reload();
    await expect(page.getByText("9 of 9 generated").first()).toBeVisible({
      timeout: 20_000,
    });
    await expect(page.getByRole("button", { name: /Candidate /i })).toHaveCount(
      9,
    );
    await expect(
      page.getByRole("button", { name: /Generate remaining/ }),
    ).toHaveCount(0);

    await page
      .getByRole("button", { name: "Regenerate selected proof" })
      .click();
    await expect(page.getByText("9 of 9 generated").first()).toBeVisible({
      timeout: 20_000,
    });
    await expect(page.getByRole("button", { name: /Candidate /i })).toHaveCount(
      9,
    );
    await expect(
      page.getByRole("button", { name: /Generate remaining/ }),
    ).toHaveCount(0);
    await expect(downloadPackage).toBeVisible();
    expect(refinementRequested).toBeTruthy();

    await page.goto("/app/enhance?e2eAuthBypass=1");
    await expect(page).toHaveURL(/\/app\/enhance\?e2eAuthBypass=1$/);
    await expect(page.getByText("9 of 9 generated").first()).toBeVisible({
      timeout: 20_000,
    });
    await expect(page.getByRole("button", { name: /Candidate /i })).toHaveCount(
      9,
    );
    await expect(page.locator(".candidate-proof img").first()).toHaveAttribute(
      "src",
      /\/profile-images\/dev\/generated\/user-1\/refinement-999\.jpg$/,
    );
    await expect(downloadPackage).toBeVisible();

    await downloadPackage.click();
    await expect(
      page.getByText(/Package downloaded\. Your selected crops/i),
    ).toBeVisible();
    await expect(page.getByText("Load Images Failed")).toHaveCount(0);
    await page.screenshot({
      path: path.join(reviewDir, "export-complete-desktop.png"),
      fullPage: true,
      animations: "disabled",
    });
    await page.setViewportSize({ width: 390, height: 844 });
    await page.screenshot({
      path: path.join(reviewDir, "export-complete-mobile.png"),
      fullPage: true,
      animations: "disabled",
    });

    await page.reload();
    await expect(page.getByText("Export kit already used")).toBeVisible({
      timeout: 20_000,
    });
    await expect(
      page.getByRole("button", { name: "Export Kit Used" }),
    ).toBeDisabled();
    await expect(page.locator(".export-list")).toHaveCount(0);
    expect(requestedOutputs).toBe(5);
  });

  test("expired preview offers a single recovery action without exposing upgrade continuation", async ({
    page,
  }) => {
    await installCommonRoutes(page, {
      openAIHeadshotMvp: true,
      profilePhotoWorkflowOverhaul: true,
      outcomePackagesVisible: true,
      profilePhotoScoreVisible: true,
      creativeStylePackVisible: true,
      premiumAugmentationsVisible: true,
      replicateTrainingFlowVisible: false,
    });

    await page.route("**/api/profilephotoworkflow/entitlements", (route) =>
      fulfillJson(route, {
        success: true,
        data: [],
        error: null,
      }),
    );
    await page.route("**/api/headshots/resumable-preview**", (route) =>
      fulfillJson(route, {
        success: true,
        data: {
          processedImageId: 77,
          imageUrl: "data:image/png;base64,iVBORw0KGgo=",
          storagePath: "dev/generated/user-1/expired.png",
          sourceStoragePath: "dev/uploads/user-1/expired-source.png",
          style: "LinkedIn",
          createdAt: new Date().toISOString(),
          hasRawPreview: false,
          canPromotePreview: false,
          activePackageCode: null,
          remainingCandidateCount: 0,
          promotedCandidate: null,
          message: "This preview can no longer continue into a paid package.",
        },
        error: null,
      }),
    );

    await page.goto("/app/enhance?e2eAuthBypass=1");
    await page
      .getByRole("button", { name: /Accept All/i })
      .click()
      .catch(() => undefined);

    await expect(
      page.getByText(/can no longer continue into a paid package/i),
    ).toBeVisible({ timeout: 20_000 });
    await expect(
      page.getByRole("button", { name: "Start with a new photo" }),
    ).toBeVisible();
    await expect(
      page.getByRole("button", {
        name: /Resume and generate paid candidates/i,
      }),
    ).toHaveCount(0);
  });

  test("feature flags hide package, score, creative, and premium UI without breaking headshot generation", async ({
    page,
  }) => {
    await installCommonRoutes(page, {
      openAIHeadshotMvp: true,
      profilePhotoWorkflowOverhaul: true,
      outcomePackagesVisible: false,
      profilePhotoScoreVisible: false,
      creativeStylePackVisible: false,
      premiumAugmentationsVisible: false,
      replicateTrainingFlowVisible: false,
    });

    await page.route("**/api/headshots/generate", (route) =>
      fulfillJson(route, {
        success: true,
        data: {
          success: true,
          imageUrl: "data:image/png;base64,iVBORw0KGgo=",
          storagePath: "dev/generated/user-1/flagged.png",
          processedImageId: 202,
          provider: "openai",
          model: "gpt-image-2",
          style: "general_professional",
          background: "auto",
          creditsCost: 1,
          remainingCredits: 4,
          correlationId: "flags",
        },
        error: null,
      }),
    );

    await openWorkspaceWithPhoto(page);
    await expect(page.getByText("Package Scope")).toHaveCount(0);
    await expect(page.getByText(/Professional readiness:/)).toHaveCount(0);
    await expect(page.getByText("Cartoon Mode")).toHaveCount(0);
    await expect(page.getByText("Premium Add-ons")).toHaveCount(0);
    await expect(page.getByText("Linkedin").first()).toBeVisible();

    await page
      .getByRole("checkbox", { name: /biometric data/i })
      .check({ force: true });
    await page
      .getByRole("button", {
        name: /Generate Free Preview|Generate Candidate|Transform Photo/i,
      })
      .click();
    await expect(
      page.getByRole("heading", { name: "Your candidate set is ready" }),
    ).toBeVisible({ timeout: 10_000 });
    await expect(
      page.getByRole("button", { name: /Download Watermarked Preview/i }),
    ).toBeVisible();
  });
});

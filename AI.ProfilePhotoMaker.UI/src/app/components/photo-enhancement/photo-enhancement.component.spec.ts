import { PhotoEnhancementComponent } from './photo-enhancement.component';
import { PortraitStyleCatalogModule } from './portrait-style-catalog';
import { of, throwError } from 'rxjs';
import { HeadshotCandidate } from '../../services/headshot-generation.service';

function candidate(id: number): HeadshotCandidate {
  return {
    imageUrl: `candidate-${id}.jpg`,
    storagePath: `generated/candidate-${id}.jpg`,
    processedImageId: id,
    provider: 'openai',
    model: 'gpt-image-2',
    correlationId: `candidate-${id}`,
  };
}

function createComponent(
  packageCode: 'free_preview' | 'starter_package' | 'pro_package',
  generatedCount: number
): PhotoEnhancementComponent {
  const component = Object.create(PhotoEnhancementComponent.prototype) as PhotoEnhancementComponent;
  Object.assign(component, {
    isHeadshotMvpEnabled: true,
    selectedPackageCode: packageCode,
    packageOptions: [
      { code: 'free_preview', name: 'Free Preview', includedCandidateCount: 1 },
      { code: 'starter_package', name: 'Starter Package', includedCandidateCount: 3 },
      { code: 'pro_package', name: 'Pro Package', includedCandidateCount: 9 },
    ],
    packageEntitlements: [],
    generatedCandidates: Array.from({ length: generatedCount }, (_, index) => candidate(index + 1)),
    enhancedImage: null,
    isLoadingEntitlements: false,
  });
  return component;
}

describe('PhotoEnhancementComponent package fulfillment', () => {
  it('resets an expired bot-verification token after a premium augmentation rejection', () => {
    const component = createComponent('pro_package', 9);
    Object.assign(component, {
      arePremiumAugmentationsVisible: true,
      enhancedImage: {
        url: 'candidate.jpg',
        storagePath: 'generated/candidate.jpg',
        processedImageId: 1,
      },
      premiumAugmentations: [],
      turnstileSiteKey: 'site-key',
      turnstileToken: 'expired-token',
    });
    (component as any)._cdr = { markForCheck: () => undefined };
    (component as any)._replicateService = {
      enhancePhoto: () =>
        throwError(() => ({
          error: {
            error: {
              code: 'BotVerificationFailed',
              message: 'Bot verification failed. Please try again.',
            },
          },
        })),
    };

    component.applyPremiumAugmentation('relighting');

    expect(component.turnstileToken).toBe('');
    expect(component.errorMessage).toContain('Bot verification failed');
  });

  it('keeps Free Preview selected when a saved paid-generation draft has no entitlement', () => {
    const component = createComponent('free_preview', 0);
    const storageKey = 'photoWorkspaceInterruptedGeneration-test';
    Object.assign(component, {
      _interruptedGenerationKey: storageKey,
      photoWorkspaceSession: {
        createStoredPreviewSourceState: () => ({
          imagePreview: 'stored-preview',
          beforeImageLoadFailed: false,
        }),
      },
    });
    localStorage.setItem(
      storageKey,
      JSON.stringify({
        clientRequestId: 'interrupted-request',
        imageStoragePath: 'uploads/source.jpg',
        styleName: 'executive',
        packageCode: 'pro_package',
        useCaseCode: 'linkedin_executive',
        isRegeneration: false,
        startedAt: new Date().toISOString(),
      })
    );

    try {
      (component as any).restoreInterruptedGeneration();

      expect(component.selectedPackageCode).toBe('free_preview');
    } finally {
      localStorage.removeItem(storageKey);
    }
  });

  it('only shows styles suited to the selected use case as recommended', () => {
    const component = createComponent('free_preview', 0);
    Object.assign(component, {
      selectedStyleGroup: 'recommended',
      selectedUseCaseCode: 'realtor',
      packUseCases: [
        {
          code: 'realtor',
          recommendedStyles: ['linkedin', 'executive', 'entrepreneur', 'startup'],
        },
      ],
      portraitStyles: [
        'linkedin',
        'executive',
        'entrepreneur',
        'startup',
        'medical',
        'academic',
        'creative',
      ].map((key, displayOrder) => ({
        key,
        style: { name: key },
        group: 'recommended',
        displayOrder,
        name: key,
      })),
      portraitStyleCatalog: new PortraitStyleCatalogModule(),
    });

    expect(component.getVisiblePortraitStyles().map(style => style.key)).toEqual([
      'linkedin',
      'executive',
      'entrepreneur',
      'startup',
    ]);
  });

  it('shows the remaining paid candidate slots as the primary generation action', () => {
    const component = createComponent('pro_package', 1);

    expect(component.getPackageProgressText()).toBe('1 of 9 generated');
    expect(component.getRemainingCandidateSlots()).toBe(8);
    expect(component.getPrimaryCtaLabel()).toBe('Generate remaining 8 photos');
    expect(component.isPaidPackageFulfillmentPending()).toBeTrue();
    expect(component.canShowFinishingTools()).toBeFalse();
  });

  it('marks a paid candidate set complete only when every slot is restored', () => {
    const component = createComponent('starter_package', 3);

    expect(component.getPackageProgressText()).toBe('3 of 3 generated');
    expect(component.getRemainingCandidateSlots()).toBe(0);
    expect(component.isCandidateFulfillmentComplete()).toBeTrue();
    expect(component.isPaidPackageFulfillmentPending()).toBeFalse();
    expect(component.canShowFinishingTools()).toBeTrue();
  });

  it('keeps partial paid fulfillment resumable', () => {
    const component = createComponent('pro_package', 4);

    expect(component.getPackageProgressText()).toBe('4 of 9 generated');
    expect(component.getPrimaryCtaLabel()).toBe('Generate remaining 5 photos');
    expect(component.isCandidateFulfillmentComplete()).toBeFalse();
  });

  it('uses the remaining Pro allowance after its promoted preview is no longer available', () => {
    const component = createComponent('pro_package', 0);
    component.enhancementType = 'headshot';
    component.packageEntitlements = [
      {
        id: 1,
        packageCode: 'pro_package',
        packageName: 'Pro Package',
        status: 'active',
        remainingPackageUses: 1,
        remainingCandidates: 8,
        remainingRefinements: 5,
        remainingPremiumAugmentations: 2,
        platformExportKitAvailable: true,
      },
    ];

    expect(component.getPackageProgressText()).toBe('1 of 9 generated');
    expect(component.getCandidateRequestCountForSelectedPackage()).toBe(8);
    expect(component.hasEnoughCredits()).toBeTrue();
  });

  it('reports an allowance mismatch without directing the user to refinements', () => {
    const component = createComponent('pro_package', 4);
    component.packageEntitlements = [
      {
        id: 1,
        packageCode: 'pro_package',
        packageName: 'Pro Package',
        status: 'active',
        remainingPackageUses: 0,
        remainingCandidates: 0,
        remainingRefinements: 3,
        remainingPremiumAugmentations: 3,
        platformExportKitAvailable: true,
      },
    ];

    expect(component.getFulfillmentBlockerText()).toContain(
      'candidate allowance does not match the unfinished package'
    );
    expect(component.getFulfillmentBlockerText()).toContain('without using a refinement');
  });

  it('describes candidate selection without relying on visual marks', () => {
    const component = createComponent('starter_package', 1);
    const scoredCandidate = {
      ...candidate(1),
      recommendationScore: 91,
    };

    expect(component.getCandidateAccessibleLabel(scoredCandidate, 0)).toBe(
      'Candidate 1, recommended best shot, score 91 out of 100'
    );
  });

  it('caps restored candidates at the package definition count', () => {
    const component = createComponent('starter_package', 5);

    expect(component.getGeneratedCandidateCount()).toBe(3);
    expect(component.getPackageProgressText()).toBe('3 of 3 generated');
  });

  it('merges a partial retry response without dropping restored candidates', () => {
    const component = createComponent('pro_package', 4);
    component.selectedCandidateId = 1;
    const response = [
      candidate(1),
      ...Array.from({ length: 5 }, (_, index) => candidate(index + 5)),
    ];

    const selected = (component as any).mergeGeneratedCandidates(response);

    expect(component.generatedCandidates.map(item => item.processedImageId)).toEqual([
      1, 2, 3, 4, 5, 6, 7, 8, 9,
    ]);
    expect(selected.processedImageId).toBe(1);
    expect(component.getPackageProgressText()).toBe('9 of 9 generated');
  });

  it('replaces the watermarked preview when paid promotion returns its private candidate', () => {
    const component = createComponent('starter_package', 1);
    const watermarked = candidate(1);
    component.generatedCandidates = [watermarked];
    component.previewCandidate = watermarked;
    const promoted = { ...candidate(2), storagePath: 'test/generated-private/user-1/raw.png' };

    const selected = (component as any).mergeGeneratedCandidates([promoted, candidate(3)]);

    expect(component.generatedCandidates.map(item => item.processedImageId)).toEqual([2, 3]);
    expect(component.previewCandidate?.processedImageId).toBe(2);
    expect(selected?.processedImageId).toBe(3);
  });

  it('merges one persisted candidate at a time without duplicates after an interrupted batch', () => {
    const component = createComponent('pro_package', 1);
    component.selectedCandidateId = 1;

    for (let id = 2; id <= 9; id++) {
      (component as any).mergeGeneratedCandidates([candidate(id)]);
    }
    // Retrying the last request must return its persisted candidate, not create a tenth slot.
    (component as any).mergeGeneratedCandidates([candidate(9)]);

    expect(component.generatedCandidates.map(item => item.processedImageId)).toEqual([
      1, 2, 3, 4, 5, 6, 7, 8, 9,
    ]);
    expect(component.getRemainingCandidateSlots()).toBe(0);
  });

  it('replaces only the selected slot after a refinement', () => {
    const component = createComponent('pro_package', 9);
    component.selectedCandidateId = 4;

    const replacement = (component as any).replaceRegeneratedCandidate([candidate(999)], 4);

    expect(component.generatedCandidates).toHaveSize(9);
    expect(component.generatedCandidates.map(item => item.processedImageId)).not.toContain(4);
    expect(component.generatedCandidates.map(item => item.processedImageId)).toContain(999);
    expect(replacement.processedImageId).toBe(999);
    expect(component.getPackageProgressText()).toBe('9 of 9 generated');
  });

  it('keeps an expired paid preview in the start-over recovery state', () => {
    const component = createComponent('pro_package', 0);
    const preview = {
      processedImageId: 42,
      imageUrl: '/profile-images/generated/preview.png',
      storagePath: 'generated/preview.png',
      sourceStoragePath: 'uploads/source.png',
      style: 'linkedin',
      createdAt: '2026-08-28T00:00:00Z',
      hasRawPreview: false,
      canPromotePreview: false,
      activePackageCode: 'pro_package',
      remainingCandidateCount: 8,
      message: 'Your package is active, but this preview asset expired. Start a new photo set.',
    };
    (component as any)._headshotGenerationService = {
      getResumablePreview: () => of({ success: true, data: preview }),
    };
    (component as any)._cdr = { markForCheck: () => undefined };
    spyOn(component, 'resumePreview');

    (component as any).loadResumablePreview(undefined, true);

    expect(component.resumablePreview).toEqual(preview);
    expect(component.resumePreview).not.toHaveBeenCalled();
  });

  it('restores persisted candidates before resuming an interrupted batch after reload', async () => {
    const component = createComponent('pro_package', 0);
    const draft = {
      clientRequestId: 'interrupted-last-request',
      imageStoragePath: 'uploads/source.png',
      styleName: 'linkedin',
      packageCode: 'pro_package',
      useCaseCode: 'linkedin_executive',
      isRegeneration: false,
      startedAt: '2026-08-28T00:00:00Z',
    };
    const preview = { hasRawPreview: true, activePackageCode: 'pro_package' };
    component.interruptedGeneration = draft as any;
    (component as any)._headshotGenerationService = {
      getResumablePreview: () => of({ success: true, data: preview }),
    };
    (component as any)._cdr = { markForCheck: () => undefined };
    spyOn(component, 'resumePreview').and.returnValue(Promise.resolve());
    spyOn(component as any, 'selectPortraitStyleByName');
    spyOn(component, 'canStartEnhancement').and.returnValue(true);
    spyOn(component, 'startEnhancement').and.returnValue(Promise.resolve());

    component.resumeInterruptedGeneration();
    await Promise.resolve();

    expect(component.resumePreview).toHaveBeenCalledWith(preview as any);
    expect(component.startEnhancement).toHaveBeenCalled();
  });

  it('ends an expired session when candidate generation returns 401', () => {
    const component = createComponent('pro_package', 1);
    const logout = jasmine.createSpy('logout');
    (component as any)._authService = { logout };
    (component as any)._cdr = { detectChanges: () => undefined };
    spyOn(console, 'error');

    (component as any).handleEnhancementFailure({
      status: 401,
      message: 'Unauthorized',
      error: { message: 'Unauthorized' },
    });

    expect(logout).toHaveBeenCalled();
  });

  it('shows verification recovery when generation rejects an unconfirmed email', () => {
    const component = createComponent('pro_package', 1);
    const logout = jasmine.createSpy('logout');
    (component as any)._authService = { logout };
    (component as any)._cdr = { detectChanges: () => undefined };
    spyOn(console, 'error');

    (component as any).handleEnhancementFailure({
      status: 401,
      error: {
        error: {
          code: 'EmailNotVerified',
          message: 'Please verify your email address before generating a headshot.',
        },
      },
    });

    expect(component.isEmailConfirmed).toBeFalse();
    expect(component.verificationMessage).toContain('verify your email address');
    expect(logout).not.toHaveBeenCalled();
  });

  it('clears source scoring state when the scoring request errors', () => {
    const component = createComponent('free_preview', 0);
    const file = new File(['photo'], 'photo.jpg', { type: 'image/jpeg' });
    (component as any).isProfilePhotoScoreVisible = true;
    component.selectedFile = file;
    (component as any)._selectedFileToken = 1;
    (component as any)._profileWorkflowService = {
      scorePhoto: () => throwError(() => new Error('score failed')),
    };
    (component as any)._cdr = { markForCheck: () => undefined };
    spyOn(console, 'warn');

    (component as any).scoreSelectedPhoto(file, 1);

    expect(component.isScoringPhoto).toBeFalse();
  });

  it('clears generated scoring state when the scoring request errors', () => {
    const component = createComponent('pro_package', 9);
    (component as any)._profileWorkflowService = {
      scoreProcessedImage: () => throwError(() => new Error('score failed')),
    };
    (component as any)._cdr = { markForCheck: () => undefined };
    spyOn(console, 'warn');

    (component as any).scoreGeneratedPhoto(1);

    expect(component.isScoringGeneratedPhoto).toBeFalse();
  });
});

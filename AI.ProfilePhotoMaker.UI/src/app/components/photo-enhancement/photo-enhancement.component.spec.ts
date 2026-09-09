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
  for (const raw of ['{', '{"clientRequestId":"unresolved"}']) {
    it(`preserves unreadable recovery state and blocks new generation: ${raw}`, async () => {
      const component = createComponent('pro_package', 9);
      const key = 'unreadable-request-regression';
      Object.assign(component, { _interruptedGenerationKey: key });
      localStorage.setItem(key, raw);
      try {
        (component as any).restoreInterruptedGeneration();
        expect(component.unrecoverableGenerationDraft).toBeTrue();
        expect(component.canStartEnhancement()).toBeFalse();
        await component.startEnhancement();
        component.enhanceAnother();
        component.discardInterruptedGeneration();
        expect(localStorage.getItem(key)).toBe(raw);
      } finally { localStorage.removeItem(key); }
    });
  }

  it('does not treat a later validation rejection as reconciliation of an unknown outcome', () => {
    const component = createComponent('pro_package', 9);
    const key = 'unknown-then-validation';
    const draft = { clientRequestId: 'unresolved-request' };
    Object.assign(component, { interruptedGeneration: draft, _interruptedGenerationKey: key,
      _cdr: { detectChanges: () => undefined } });
    try {
      (component as any).handleEnhancementFailure({ status: 502, error: { error: { code: 'ProviderOutcomeUnknown' } } });
      (component as any).handleEnhancementFailure({ status: 400, error: { error: { code: 'InvalidImageSource' } } });
      expect(component.interruptedGeneration?.clientRequestId).toBe(draft.clientRequestId);
      expect(localStorage.getItem(key)).not.toBeNull();
    } finally { localStorage.removeItem(key); }
  });

  it('blocks ordinary generation while a request is unresolved', async () => {
    const component = createComponent('pro_package', 9);
    const draft = { clientRequestId: 'unresolved-request' };
    Object.assign(component, { interruptedGeneration: draft, isProcessing: false,
      isApplyingPremiumAugmentation: false });
    spyOn(component as any, 'hasEnoughCredits').and.returnValue(true);
    spyOn(component as any, 'uploadImageForEnhancement').and.rejectWith(new Error('must not upload'));
    expect(component.canStartEnhancement()).toBeFalse();
    await component.startEnhancement();
    await component.startEnhancement('another-request');
    expect((component as any).uploadImageForEnhancement).not.toHaveBeenCalled();
    expect(component.interruptedGeneration).toBe(draft as any);
  });

  it('cannot discard an unresolved request after processing stops', () => {
    const component = createComponent('pro_package', 9);
    const draft = { clientRequestId: 'unresolved-request' };
    Object.assign(component, { interruptedGeneration: draft, isProcessing: false,
      isApplyingPremiumAugmentation: false, _cdr: { markForCheck: () => undefined } });
    spyOn(component, 'enhanceAnother');
    component.discardInterruptedGeneration();
    expect(component.interruptedGeneration).toBe(draft as any);
    expect(component.enhanceAnother).not.toHaveBeenCalled();
  });

  it('restores unresolved request identity even after 24 hours', () => {
    const component = createComponent('pro_package', 9);
    const key = 'aged-request-regression';
    const draft = { clientRequestId: 'old-unresolved-request', imageStoragePath: 'generated/user/saved.png',
      styleName: 'linkedin', startedAt: new Date(Date.now() - 48 * 60 * 60 * 1000).toISOString() };
    Object.assign(component, { _interruptedGenerationKey: key, photoWorkspaceSession: {
      createStoredPreviewSourceState: () => ({ imagePreview: 'saved', beforeImageLoadFailed: false }),
    } });
    spyOn(component as any, 'getStorageProxyUrl').and.returnValue('/saved');
    localStorage.setItem(key, JSON.stringify(draft));
    try {
      (component as any).restoreInterruptedGeneration();
      expect(component.interruptedGeneration).toEqual(jasmine.objectContaining(draft));
      expect(component.interruptedGeneration?.outcomeUncertain).toBeTrue();
      expect((component as any)._activeGenerationClientRequestId).toBe(draft.clientRequestId);
      expect(localStorage.getItem(key)).not.toBeNull();
    } finally { localStorage.removeItem(key); }
  });

  for (const state of ['processing', 'premium', 'saved']) {
    it(`does not reset a photo with ${state} work`, () => {
      const component = createComponent('pro_package', 9);
      const image = { url: 'saved-photo', displayUrl: 'saved-photo' };
      const draft = state === 'saved' ? { clientRequestId: 'pending-request' } : null;
      Object.assign(component, {
        enhancedImage: image, interruptedGeneration: draft,
        isProcessing: state === 'processing', isApplyingPremiumAugmentation: state === 'premium',
      });
      spyOn(component, 'resetAdjustments').and.stub();
      component.enhanceAnother();
      expect(component.enhancedImage).toBe(image);
      expect(component.interruptedGeneration).toBe(draft as any);
      expect(component.resetAdjustments).not.toHaveBeenCalled();
    });
  }

  for (const [status, code, rejectedBeforeGeneration] of [
    [400, 'InvalidImageSource', true],
    [400, 'InvalidRefinement', true],
    [409, 'GenerationInProgress', false],
    [502, 'ProviderOutcomeUnknown', false],
    [0, 'NetworkError', false],
  ] as const) {
    it(`releases only definitively rejected saved requests (${code})`, () => {
      const component = createComponent('pro_package', 9);
      const draft = { clientRequestId: 'saved-request', refinementCode: 'upright_posture' };
      Object.assign(component, { interruptedGeneration: draft, isProcessing: true });
      (component as any)._interruptedGenerationKey = 'rejected-refinement-test';
      (component as any)._activeGenerationClientRequestId = 'saved-request';
      (component as any)._cdr = { detectChanges: () => undefined };
      localStorage.setItem('rejected-refinement-test', JSON.stringify(draft));
      (component as any).handleEnhancementFailure({ status, message: 'Request failed', error: { error: { code, message: 'Choose a valid photo.' } } });
      expect(component.isProcessing).toBeFalse();
      expect(component.interruptedGeneration).toEqual(rejectedBeforeGeneration ? null : draft as any);
      expect(localStorage.getItem('rejected-refinement-test') === null).toBe(rejectedBeforeGeneration);
      localStorage.removeItem('rejected-refinement-test');
    });
  }

  it('can retrieve a saved refinement receipt after the last allowance was consumed', () => {
    const component = createComponent('pro_package', 9);
    Object.assign(component, {
      isEmailConfirmed: true, biometricConsentAccepted: true, enhancementType: 'headshot',
      turnstileSiteKey: '',
      packageEntitlements: [{ packageCode: 'pro_package', status: 'active',
        remainingCandidates: 0, remainingPackageUses: 0, remainingRefinements: 0 }],
      interruptedGeneration: {
        clientRequestId: 'same-request', imageStoragePath: 'saved/proof.png',
        styleName: 'linkedin', packageCode: 'pro_package', useCaseCode: 'linkedin_executive',
        isRegeneration: true, refinementCode: 'subtle_smile', replacesProcessedImageId: 99,
        candidateSlotId: 4, startedAt: new Date().toISOString(),
      },
    });
    (component as any)._cdr = { markForCheck: () => undefined };
    (component as any).selectPortraitStyleByName = () => undefined;
    (component as any).getStorageProxyUrl = (path: string) => path;
    (component as any).createEnhancedImageViewModel = (url: string, type: string, processedImageId: number, storagePath: string) => ({ url, type, processedImageId, storagePath });
    const start = spyOn(component, 'startEnhancement').and.resolveTo();
    expect(component.canResumeInterruptedGeneration()).toBeTrue();
    component.resumeInterruptedGeneration();
    expect(start).toHaveBeenCalledOnceWith('same-request');
    expect(component.canStartEnhancement(true, true)).toBeTrue();
    expect(component.canStartEnhancement(true)).toBeFalse();
    expect(component.canStartRegeneration()).toBeFalse();
    expect((component as any)._activeGenerationClientRequestId).toBe('same-request');
    component.selectedRefinementCode = 'upright_posture';
    expect(component.canStartEnhancement(true, true)).toBeFalse();
  });

  for (const enhancementType of ['headshot', 'headshot_linkedin']) {
    it(`sends a deliberate selected-photo edit through the fenced API (${enhancementType})`, async () => {
      const component = createComponent('pro_package', 9);
      Object.assign(component, {
        enhancementType, selectedFile: null, previewSourceStoragePath: null,
        selectedPortraitStyle: null, selectedCandidateId: 4,
        enhancedImage: { processedImageId: 99, storagePath: 'generated/premium-result.png', url: 'selected.jpg', displayUrl: 'selected.jpg' },
        isEmailConfirmed: true, biometricConsentAccepted: true,
        turnstileSiteKey: '', isProfilePhotoScoreVisible: false, arePremiumAugmentationsVisible: true,
        packageEntitlements: [{ packageCode: 'pro_package', status: 'active',
          remainingCandidates: 0, remainingPackageUses: 0, remainingRefinements: 4,
          remainingPremiumAugmentations: 0 }],
      });
      expect(component.canStartRegeneration()).toBeFalse();
      component.selectedRefinementCode = 'subtle_smile';
      expect(component.canStartRegeneration()).toBeTrue();
      expect(component.canApplyPremiumAugmentation()).toBeFalse();
      let sent: any;
      (component as any)._headshotGenerationService = {
        generateHeadshot: (request: any) => {
          sent = request;
          return of({ success: true, data: { ...candidate(100), candidates: [candidate(100)] } });
        },
      };
      (component as any)._cdr = { detectChanges: () => undefined, markForCheck: () => undefined };
      (component as any)._stateService = { refreshGeneratedPhotosCount: () => undefined };
      (component as any)._analytics = { trackEvent: () => undefined };
      (component as any)._interruptedGenerationKey = 'guided-refinement-test';
      (component as any).createEnhancedImageViewModel = (url: string, type: string, processedImageId: number, storagePath: string) => ({ url, type, processedImageId, storagePath });
      spyOn(component as any, 'loadAuthorizedCandidateImages').and.callFake(async (images: any) => images);
      const upload = spyOn(component as any, 'uploadImageForEnhancement');
      spyOn(component as any, 'refreshCreditState').and.resolveTo();
      (component as any)._nextRequestIsRegeneration = true;
      await component.startEnhancement();
      expect(sent).toEqual(jasmine.objectContaining({
        imageStoragePath: 'generated/premium-result.png', replacesProcessedImageId: 99,
        refinementCode: 'subtle_smile', numOutputs: 1, isRegeneration: true,
      }));
      expect(upload).not.toHaveBeenCalled();
      expect(component.enhancedImage?.previousDisplayUrl).toBe('selected.jpg');
      expect(component.generatedCandidates.length).toBe(9);
      expect(component.generatedCandidates[3].processedImageId).toBe(100);
      expect(component.selectedRefinementCode).toBeNull();
      expect(localStorage.getItem('guided-refinement-test')).toBeNull();
    });
  }

  it('sends only the storage path when legacy enhancement upload returns a relative display URL', async () => {
    const component = createComponent('free_preview', 0);
    let request: any;
    Object.assign(component, {
      isHeadshotMvpEnabled: false,
      enhancementType: 'professional',
      selectedFile: new File(['photo'], 'source.jpg', { type: 'image/jpeg' }),
      isEmailConfirmed: true,
      biometricConsentAccepted: true,
      isProfilePhotoScoreVisible: false,
      turnstileSiteKey: '',
    });
    spyOn(component, 'hasEnoughCredits').and.returnValue(true);
    spyOn(component as any, 'uploadImageForEnhancement').and.resolveTo({
      url: '/profile-images/uploads/source.jpg',
      fileName: 'source.jpg',
      storagePath: 'uploads/source.jpg',
    });
    (component as any)._cdr = { detectChanges: () => undefined, markForCheck: () => undefined };
    (component as any)._stateService = { refreshCredits: () => Promise.resolve() };
    (component as any).createEnhancedImageViewModel = (
      url: string,
      type: string,
      id: number,
      storagePath: string
    ) => ({
      url,
      type,
      processedImageId: id,
      storagePath,
    });
    (component as any)._replicateService = {
      enhancePhoto: (value: any) => {
        request = value;
        return of({
          success: true,
          data: {
            provider: 'OpenAI',
            Status: 'succeeded',
            Output: ['https://example.test/enhanced.jpg'],
            processedImageId: 1,
            storagePath: 'enhanced/result.jpg',
          },
        });
      },
    };

    await component.startEnhancement();

    expect(request.imageStoragePath).toBe('uploads/source.jpg');
    expect(request.imageUrl).toBeUndefined();
  });

  it('resets an expired bot-verification token after a premium augmentation rejection', () => {
    const component = createComponent('pro_package', 9);
    Object.assign(component, {
      arePremiumAugmentationsVisible: true,
      enhancedImage: {
        url: 'candidate.jpg',
        storagePath: 'generated/candidate.jpg',
        processedImageId: 1,
      },
      premiumAugmentations: [{ type: 'relighting', label: 'Relighting', options: [] }],
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

    spyOn(component, 'hasPremiumAugmentationEntitlement').and.returnValue(true);
    component.applyPremiumAugmentation('relighting');

    expect(component.turnstileToken).toBe('');
    expect(component.errorMessage).toContain('Bot verification failed');
  });

  it('sends the selected premium direction instead of applying a generic upgrade', () => {
    const component = createComponent('pro_package', 9);
    let request: any;
    Object.assign(component, {
      arePremiumAugmentationsVisible: true,
      enhancedImage: {
        url: 'candidate.jpg',
        storagePath: 'generated/candidate.jpg',
        processedImageId: 1,
      },
      premiumAugmentations: [
        {
          label: 'Background upgrade',
          type: 'background_upgrade',
          description: 'Choose a setting.',
          options: [
            {
              id: 'modern-office',
              label: 'Modern office',
              description: 'A credible workplace.',
              prompt: 'Replace only the background with a softly blurred modern office.',
            },
          ],
        },
      ],
      isProfilePhotoScoreVisible: false,
      areOutcomePackagesVisible: false,
    });
    (component as any)._cdr = { markForCheck: () => undefined };
    (component as any)._stateService = { refreshCredits: () => Promise.resolve() };
    (component as any).createEnhancedImageViewModel = (
      url: string,
      type: string,
      id: number,
      storagePath: string
    ) => ({
      url,
      type,
      processedImageId: id,
      storagePath,
    });
    (component as any)._replicateService = {
      enhancePhoto: (value: any) => {
        request = value;
        return of({
          success: true,
          data: { dataUrl: 'result.jpg', processedImageId: 2, storagePath: 'enhanced/result.jpg' },
        });
      },
    };

    spyOn(component, 'hasPremiumAugmentationEntitlement').and.returnValue(true);
    component.selectPremiumAugmentation('background_upgrade');
    component.applySelectedPremiumAugmentation();

    expect(request.customPrompt).toBe(
      'Replace only the background with a softly blurred modern office.'
    );
    expect(request.enhancementType).toBe('background_upgrade');
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

  it('loads the authorized Gallery source into Studio refinement', () => {
    const component = createComponent('pro_package', 0);
    const getStudioImageSource = jasmine.createSpy('getStudioImageSource').and.returnValue(
      of({
        success: true,
        data: {
          processedImageId: 42,
          storagePath: 'generated/gallery-photo.png',
          imageUrl: 'gallery-photo-url',
          style: 'linkedin',
        },
      })
    );
    (component as any)._profileWorkflowService = { getStudioImageSource };
    (component as any)._cdr = { markForCheck: () => undefined };
    (component as any).imageViewModule = {
      createImageView: (
        url: string,
        type?: string,
        processedImageId?: number,
        storagePath?: string
      ) => ({
        url,
        type,
        processedImageId,
        storagePath,
      }),
    };

    component.packageEntitlements = [
      {
        id: 1,
        packageCode: 'pro_package',
        packageName: 'Pro Package',
        status: 'active',
        remainingPackageUses: 1,
        remainingCandidates: 7,
        remainingRefinements: 1,
        remainingPremiumAugmentations: 0,
        platformExportKitAvailable: true,
      },
    ];

    (component as any).loadGalleryImageForRefinement(42);

    expect(getStudioImageSource).toHaveBeenCalledOnceWith(42);
    expect(component.enhancementType).toBe('headshot_linkedin');
    expect(component.selectedCandidateId).toBe(42);
    expect(component.saveSuccessMessage).toContain('Photo loaded from your workspace');
    expect(component.canShowFinishingTools()).toBeTrue();
    expect(component.isPaidPackageFulfillmentPending()).toBeFalse();
    expect(component.getPackageTicketStatus()).toBe('Refining saved photo');
    spyOn(component, 'canStartEnhancement').and.returnValue(true);
    expect(component.canStartRegeneration()).toBeFalse();
    component.selectedRefinementCode = 'subtle_smile';
    expect(component.canStartRegeneration()).toBeTrue();
  });

  it('uses a paid refinement allowance instead of legacy credits for a Gallery photo', () => {
    const component = createComponent('pro_package', 0);
    Object.assign(component, {
      isHeadshotMvpEnabled: true,
      packageEntitlements: [
        {
          packageCode: 'pro_package',
          status: 'active',
          remainingPackageUses: 1,
          remainingCandidates: 7,
          remainingRefinements: 1,
          remainingPremiumAugmentations: 0,
          platformExportKitAvailable: true,
        },
      ],
    });
    component.enhancementType = 'headshot_linkedin';

    expect(component.hasEnoughCredits()).toBeTrue();
  });

  it('shows the API recovery message instead of a generic HTTP error', () => {
    const component = createComponent('pro_package', 0);

    const message = (component as any).getEnhancementErrorMessage({
      status: 400,
      message: 'Http failure response',
      error: {
        error: { message: 'The selected source photo expired. Choose a saved candidate instead.' },
      },
    });

    expect(message).toBe('The selected source photo expired. Choose a saved candidate instead.');
  });

  it('restores paid candidates even when the preview source has expired', () => {
    const component = createComponent('pro_package', 0);
    const preview = {
      hasRawPreview: false,
      activePackageCode: 'pro_package',
      candidates: [candidate(1)],
    } as any;
    Object.assign(component, {
      _headshotGenerationService: {
        getResumablePreview: () => of({ success: true, data: preview }),
      },
      _cdr: { markForCheck: () => undefined },
    });
    spyOn(component as any, 'clearInterruptedGeneration');
    const resume = spyOn(component, 'resumePreview');

    (component as any).loadResumablePreview();

    expect(resume).toHaveBeenCalledWith(preview);
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

  it('does not reconcile an unknown request merely because other candidates exist', () => {
    const component = createComponent('pro_package', 0);
    const draft = { clientRequestId: 'unknown-request' };
    Object.assign(component, { interruptedGeneration: draft,
      _cdr: { markForCheck: () => undefined },
      _headshotGenerationService: { getResumablePreview: () => of({ success: true,
        data: { candidates: [candidate(42)], activePackageCode: 'pro_package' } }) },
    });
    spyOn(component, 'resumePreview');
    (component as any).loadResumablePreview();
    expect(component.interruptedGeneration).toBe(draft as any);
  });

  it('restores saved candidates without retaining an expired source or discarding unresolved work', async () => {
    const component = createComponent('pro_package', 0);
    Object.assign(component, {
      photoWorkspaceSession: {
        createStoredPreviewSourceState: (
          sourceStoragePath: string | null,
          imagePreview: string | null
        ) => ({
          imagePreview,
          beforeImageLoadFailed: false,
          currentSourceStoragePath: sourceStoragePath,
          previewSourceStoragePath: sourceStoragePath,
          previewStyleName: null,
        }),
      },
      portraitStyleCatalog: { findByStyleName: () => null },
      portraitStyles: [],
      isProfilePhotoScoreVisible: false,
      interruptedGeneration: { clientRequestId: 'unknown-request' },
      _interruptedGenerationKey: 'expired-source-resume',
      _cdr: { markForCheck: () => undefined },
    });
    spyOn(component as any, 'loadAuthorizedCandidateImages').and.resolveTo([candidate(42)]);
    spyOn(component, 'selectCandidate');

    await component.resumePreview({
      processedImageId: 42,
      imageUrl: 'preview.jpg',
      storagePath: 'generated/preview.jpg',
      sourceStoragePath: 'uploads/expired-source.jpg',
      sourceAvailable: false,
      style: 'linkedin',
      createdAt: '2026-08-28T00:00:00Z',
      hasRawPreview: true,
      canPromotePreview: false,
      activePackageCode: 'pro_package',
      remainingCandidateCount: 7,
      candidates: [candidate(42)],
    });

    expect(component.previewSourceStoragePath).toBeNull();
    expect(component.currentSourceStoragePath).toBeNull();
    expect((component as any).hasEnhancementSourceReady()).toBeFalse();
    expect(component.saveSuccessMessage).toContain('original upload expired');
    expect(component.interruptedGeneration?.clientRequestId).toBe('unknown-request');
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

import { PhotoEnhancementComponent } from './photo-enhancement.component';
import { throwError } from 'rxjs';
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

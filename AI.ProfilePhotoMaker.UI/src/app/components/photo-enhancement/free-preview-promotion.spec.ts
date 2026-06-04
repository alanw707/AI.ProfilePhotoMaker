import { FreePreviewPromotionModule } from './free-preview-promotion';

describe('FreePreviewPromotionModule', () => {
  const promotion = new FreePreviewPromotionModule();

  it('promotes a Free Preview into paid candidate one when package, style, and source match', () => {
    const plan = promotion.plan({
      packageCode: 'pro_package',
      totalCandidateCount: 9,
      hasPreviewCandidate: true,
      previewStyleName: 'linkedin',
      selectedStyleName: 'linkedin',
      previewSourceStoragePath: 'users/1/source.png',
      currentSourceStoragePath: 'users/1/source.png',
    });

    expect(plan.canPromotePreview).toBeTrue();
    expect(plan.remainingCandidateCount).toBe(8);
    expect(plan.continuityMessage).toContain('preview becomes candidate #1');
  });

  it('does not promote free package generation', () => {
    const plan = promotion.plan({
      packageCode: 'free_preview',
      totalCandidateCount: 1,
      hasPreviewCandidate: true,
      previewStyleName: 'linkedin',
      selectedStyleName: 'linkedin',
      previewSourceStoragePath: 'users/1/source.png',
      currentSourceStoragePath: 'users/1/source.png',
    });

    expect(plan.canPromotePreview).toBeFalse();
    expect(plan.remainingCandidateCount).toBe(1);
  });

  it('starts a full paid set when style or source changed', () => {
    const plan = promotion.plan({
      packageCode: 'starter_package',
      totalCandidateCount: 3,
      hasPreviewCandidate: true,
      previewStyleName: 'linkedin',
      selectedStyleName: 'executive',
      previewSourceStoragePath: 'users/1/source.png',
      currentSourceStoragePath: 'users/1/source.png',
    });

    expect(plan.canPromotePreview).toBeFalse();
    expect(plan.remainingCandidateCount).toBe(3);
    expect(plan.continuityMessage).toContain('starts a new paid set');
  });
});

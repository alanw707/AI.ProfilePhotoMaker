import { FreePreviewPromotionModule } from './free-preview-promotion';

describe('FreePreviewPromotionModule', () => {
  const promotion = new FreePreviewPromotionModule();

  it('promotes a Free Preview into paid candidate one when a paid package is active', () => {
    const plan = promotion.plan({
      packageCode: 'pro_package',
      totalCandidateCount: 9,
      hasPreviewCandidate: true,
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
    });

    expect(plan.canPromotePreview).toBeFalse();
    expect(plan.remainingCandidateCount).toBe(1);
  });

  it('generates the full paid set when no preview is available', () => {
    const plan = promotion.plan({
      packageCode: 'starter_package',
      totalCandidateCount: 3,
      hasPreviewCandidate: false,
    });

    expect(plan.canPromotePreview).toBeFalse();
    expect(plan.remainingCandidateCount).toBe(3);
    expect(plan.continuityMessage).toBe('Generate all 3 candidates in this paid set.');
  });
});

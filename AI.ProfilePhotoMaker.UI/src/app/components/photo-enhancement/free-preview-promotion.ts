export interface FreePreviewPromotionInput {
  packageCode: string;
  totalCandidateCount: number;
  hasPreviewCandidate: boolean;
}

export interface FreePreviewPromotionPlan {
  canPromotePreview: boolean;
  remainingCandidateCount: number;
  continuityMessage: string;
}

/**
 * Deep Free Preview promotion module.
 *
 * A paid package keeps an available preview as candidate #1 and generates only the
 * remaining candidates.
 */
export class FreePreviewPromotionModule {
  plan(input: FreePreviewPromotionInput): FreePreviewPromotionPlan {
    const canPromotePreview = this.canPromote(input);
    const remainingCandidateCount = canPromotePreview
      ? Math.max(input.totalCandidateCount - 1, 0)
      : input.totalCandidateCount;

    return {
      canPromotePreview,
      remainingCandidateCount,
      continuityMessage: canPromotePreview
        ? `Your preview becomes candidate #1 and is unwatermarked. We will generate ${remainingCandidateCount} more candidate${remainingCandidateCount === 1 ? '' : 's'}.`
        : `Generate all ${input.totalCandidateCount} candidates in this paid set.`,
    };
  }

  canPromote(input: FreePreviewPromotionInput): boolean {
    return input.packageCode !== 'free_preview' && input.hasPreviewCandidate;
  }
}

export interface FreePreviewPromotionInput {
  packageCode: string;
  totalCandidateCount: number;
  hasPreviewCandidate: boolean;
  previewStyleName: string | null;
  selectedStyleName: string | null;
  previewSourceStoragePath: string | null;
  currentSourceStoragePath: string | null;
}

export interface FreePreviewPromotionPlan {
  canPromotePreview: boolean;
  remainingCandidateCount: number;
  continuityMessage: string;
}

/**
 * Deep Free Preview promotion module.
 *
 * Interface: callers provide package/style/source facts and get the candidate reuse plan.
 * Implementation owns ADR-0003's rule that a Free Preview can become paid candidate #1 only
 * when package is paid and style/source still match.
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
        ? `Your preview becomes candidate #1. We will generate ${remainingCandidateCount} more candidate${remainingCandidateCount === 1 ? '' : 's'}.`
        : `Changing the source photo or style starts a new paid set and generates all ${input.totalCandidateCount} candidates.`,
    };
  }

  canPromote(input: FreePreviewPromotionInput): boolean {
    return !!(
      input.packageCode !== 'free_preview' &&
      input.hasPreviewCandidate &&
      input.previewStyleName &&
      input.selectedStyleName === input.previewStyleName &&
      input.previewSourceStoragePath &&
      input.currentSourceStoragePath === input.previewSourceStoragePath
    );
  }
}

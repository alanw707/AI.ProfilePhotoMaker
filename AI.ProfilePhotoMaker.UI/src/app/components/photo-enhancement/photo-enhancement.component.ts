/* eslint-disable max-lines -- legacy workspace orchestration remains in one route component; state seams are covered by focused and E2E tests */
import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ElementRef,
  OnDestroy,
  OnInit,
  ViewChild,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { ReplicateService } from '../../services/replicate.service';
import { FileUploadService } from '../../services/file-upload.service';
import {
  HeadshotCandidate,
  HeadshotGenerationService,
  ResumableHeadshotPreview,
} from '../../services/headshot-generation.service';
import { Style, StyleService } from '../../services/style.service';
import { StylePreviewService } from '../../services/style-preview.service';
import { HeaderNavigationComponent } from '../../shared/header-navigation/header-navigation.component';
import { WorkspaceStateService } from '../../services/workspace-state.service';
import { CreditService, UserCreditStatus } from '../../services/credit.service';
import { ConfigService } from '../../services/config.service';
import { AuthService } from '../../services/auth.service';
import { TurnstileComponent } from '../../shared/turnstile/turnstile.component';
import { finalize, Subscription, firstValueFrom } from 'rxjs';
import { BiometricConsentService } from '../../services/biometric-consent.service';
import { AnalyticsService } from '../../services/analytics.service';
import {
  PlatformExportOption,
  OutcomePackageDefinition,
  PackageEntitlement,
  ProfilePhotoScore,
  ProfileWorkflowService,
} from '../../services/profile-workflow.service';
import {
  PhotoWorkspaceImageView,
  PhotoWorkspaceImageViewModule,
} from './photo-workspace-image-view';
import { FreePreviewPromotionModule } from './free-preview-promotion';
import { PhotoWorkspaceSessionModule } from './photo-workspace-session';
import { PortraitStyleCatalogModule, PortraitStyleGroup } from './portrait-style-catalog';

type EnhancedImage = PhotoWorkspaceImageView;

interface CandidateViewModel extends HeadshotCandidate {
  score?: ProfilePhotoScore;
  recommendationScore?: number;
  recommendationReason?: string;
  promotedFromPreview?: boolean;
  previewUnavailable?: boolean;
}

type PackUseCaseCode = 'linkedin_executive' | 'realtor' | 'founder_press_kit';

interface PackUseCaseOption {
  code: PackUseCaseCode;
  label: string;
  shortLabel: string;
  description: string;
  recommendedStyles: string[];
  defaultExports: string[];
}

interface InterruptedGenerationDraft {
  clientRequestId: string;
  imageStoragePath: string;
  styleName: string;
  packageCode: 'free_preview' | 'starter_package' | 'pro_package';
  useCaseCode: PackUseCaseCode;
  isRegeneration: boolean;
  replacesProcessedImageId?: number;
  startedAt: string;
}

interface PortraitStyleCard {
  style: Style;
  key: string;
  name: string;
  description: string;
  previewUrl: string;
  group: PortraitStyleGroup;
  badgeLabel: string;
  displayOrder: number;
}

@Component({
  selector: 'app-photo-enhancement',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, HeaderNavigationComponent, TurnstileComponent],
  templateUrl: './photo-enhancement.component.html',
  styleUrls: ['./photo-enhancement.component.sass'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PhotoEnhancementComponent implements OnInit, OnDestroy {
  @ViewChild('fileInput') fileInput!: ElementRef<HTMLInputElement>;
  @ViewChild(TurnstileComponent) turnstile?: TurnstileComponent;

  selectedFile: File | null = null;
  imagePreview: string | null = null;
  beforeImageLoadFailed = false;
  enhancementType = 'background';
  isProcessing = false;
  processingProgress = 0;
  processingStatus = '';
  enhancedImage: EnhancedImage | null = null;
  userCreditStatus: UserCreditStatus | null = null;
  errorMessage = '';
  isDragOver = false;
  isLoadingCredits = true;
  isLoadingAccountStatus = true;
  isEmailConfirmed = true;
  isResendingVerificationEmail = false;
  verificationMessage = '';
  allowedTypes: string[] = ['image/jpeg', 'image/png', 'image/webp'];
  biometricConsentAccepted = false;
  turnstileToken = '';
  readonly turnstileSiteKey: string;
  readonly isHeadshotMvpEnabled: boolean;
  readonly isProfileWorkflowEnabled: boolean;
  readonly areOutcomePackagesVisible: boolean;
  readonly isProfilePhotoScoreVisible: boolean;
  readonly isCreativeStylePackVisible: boolean;
  readonly arePremiumAugmentationsVisible: boolean;
  private readonly imageViewModule = new PhotoWorkspaceImageViewModule({
    toApiImageUrl: (path: string) => this.toApiImageUrl(path),
  });
  private readonly photoWorkspaceSession = new PhotoWorkspaceSessionModule();
  private readonly freePreviewPromotion = new FreePreviewPromotionModule();
  private readonly portraitStyleCatalog = new PortraitStyleCatalogModule<PortraitStyleCard>();
  profileScore: ProfilePhotoScore | null = null;
  generatedScore: ProfilePhotoScore | null = null;
  isScoringPhoto = false;
  qualityGateOverrideAccepted = false;
  isScoringGeneratedPhoto = false;
  selectedRole = 'general_professional';
  selectedUseCaseCode: PackUseCaseCode = 'linkedin_executive';
  readonly packUseCases: PackUseCaseOption[] = [
    {
      code: 'linkedin_executive',
      label: 'LinkedIn / Executive',
      shortLabel: 'LinkedIn',
      description: 'Best for LinkedIn, resumes, avatars, and executive profile pages.',
      recommendedStyles: ['linkedin', 'executive', 'tech-professional', 'entrepreneur'],
      defaultExports: ['linkedin_profile', 'google_avatar', 'resume_headshot', 'original_high_res'],
    },
    {
      code: 'realtor',
      label: 'Realtor',
      shortLabel: 'Realtor',
      description: 'Trust-building portraits for Zillow, Realtor.com, flyers, and social posts.',
      recommendedStyles: ['linkedin', 'executive', 'entrepreneur', 'startup'],
      defaultExports: ['realtor_square', 'realtor_flyer', 'linkedin_profile', 'original_high_res'],
    },
    {
      code: 'founder_press_kit',
      label: 'Founder / Press Kit',
      shortLabel: 'Founder',
      description: 'Founder portraits for press bios, podcasts, website bios, and social banners.',
      recommendedStyles: ['entrepreneur', 'executive', 'startup', 'tech-professional', 'creative'],
      defaultExports: [
        'website_bio',
        'podcast_avatar',
        'founder_banner',
        'linkedin_profile',
        'original_high_res',
      ],
    },
  ];
  selectedPortraitStyle: PortraitStyleCard | null = null;
  selectedStyleGroup: PortraitStyleGroup = 'recommended';
  portraitStyles: PortraitStyleCard[] = [];
  isLoadingPortraitStyles = false;
  portraitStyleError = '';
  selectedPackageCode: 'free_preview' | 'starter_package' | 'pro_package' = 'free_preview';
  pendingPaidStyleName: string | null = null;
  previewStyleName: string | null = null;
  previewSourceStoragePath: string | null = null;
  currentSourceStoragePath: string | null = null;
  rawPreviewAvailable = false;
  previewCandidate: CandidateViewModel | null = null;
  resumablePreview: ResumableHeadshotPreview | null = null;
  isLoadingResumablePreview = false;
  generatedCandidates: CandidateViewModel[] = [];
  selectedCandidateId: number | null = null;
  private readonly candidateObjectUrls = new Set<string>();
  interruptedGeneration: InterruptedGenerationDraft | null = null;
  readonly roleOptions = [
    {
      value: 'general_professional',
      label: 'General professional',
      description: 'Balanced polish for LinkedIn, resumes, and internal directories.',
    },
    {
      value: 'founder_executive',
      label: 'Founder / executive',
      description: 'Confident framing, premium lighting, and boardroom-ready presence.',
    },
    {
      value: 'tech_engineering',
      label: 'Tech / engineering',
      description: 'Approachable, clear, and modern for teams and product profiles.',
    },
    {
      value: 'healthcare_clinical',
      label: 'Healthcare / clinical',
      description: 'Trust-first lighting with clean, calm presentation.',
    },
    {
      value: 'realtor_sales',
      label: 'Realtor / sales',
      description: 'Warm, high-trust profile photos for client-facing platforms.',
    },
    {
      value: 'creative_creator',
      label: 'Creative / creator',
      description: 'Expressive polish while keeping exports platform-ready.',
    },
    {
      value: 'legal_lawyer',
      label: 'Legal / lawyer',
      description: 'Conservative, crisp, and credible professional styling.',
    },
    {
      value: 'finance',
      label: 'Finance',
      description: 'Clean background, balanced lighting, and credibility-focused polish.',
    },
  ];
  readonly workflowOptions = [
    {
      value: 'headshot',
      title: 'Professional Profile Photo',
      subtitle: 'Role-aware headshot package',
      description: 'Best for LinkedIn, resumes, company bios, and business profiles.',
      image: 'assets/marketing/before-after/linkedin-after.jpg',
      visibleWhen: 'headshot',
    },
    {
      value: 'background',
      title: 'Background Remover',
      subtitle: 'Clean studio backdrop',
      description: 'Remove clutter and replace it with a professional background.',
      image: 'assets/marketing/before-after/executive-after.jpg',
      visibleWhen: 'creative',
    },
    {
      value: 'social',
      title: 'Social Media',
      subtitle: 'Bright and engaging',
      description: 'Polished profile photo for Instagram, creator pages, and social avatars.',
      image: 'assets/marketing/before-after/beach-vibes-after.jpg',
      visibleWhen: 'creative',
    },
    {
      value: 'cartoon',
      title: 'Cartoon Mode',
      subtitle: 'Animated portrait',
      description: 'Fun stylized transformation with a playful character feel.',
      image: 'assets/marketing/before-after/set-2-after.png',
      visibleWhen: 'creative',
    },
    {
      value: 'chibi',
      title: 'Chibi Style',
      subtitle: 'Cute anime look',
      description: 'Oversized-head anime styling for cute avatars and stickers.',
      image: 'assets/marketing/before-after/set-3-after.png',
      visibleWhen: 'creative',
    },
    {
      value: 'pixar_3d',
      title: 'Pixar 3D',
      subtitle: 'Cinematic 3D portrait',
      description: 'Premium 3D animation style with soft lighting and depth.',
      image: 'assets/marketing/before-after/academic1-after.jpg',
      visibleWhen: 'creative',
    },
  ];
  packageOptions: OutcomePackageDefinition[] = [];
  packageEntitlements: PackageEntitlement[] = [];
  isLoadingPackages = false;
  isLoadingEntitlements = false;
  readonly photoAdjustments = [
    'Zoom for profile avatars',
    'Rotate / straighten',
    'Brightness',
    'Contrast',
  ];
  readonly premiumAugmentations = [
    {
      label: 'Relighting',
      type: 'relighting',
      description: 'Balance shadows and add cleaner studio-style light.',
    },
    {
      label: 'Professional polish',
      type: 'professional_polish',
      description: 'Reduce minor distractions while keeping a natural look.',
    },
    {
      label: 'Outfit upgrade',
      type: 'outfit_upgrade',
      description: 'Try a more polished business-casual wardrobe.',
    },
    {
      label: 'Background upgrade',
      type: 'background_upgrade',
      description: 'Swap clutter for a cleaner professional setting.',
    },
    {
      label: 'Skin tone polish',
      type: 'skin_tone_polish',
      description: 'Even redness and uneven color without changing identity.',
    },
    {
      label: 'Sharpen detail',
      type: 'sharpen_detail',
      description: 'Improve crispness around eyes, hair, and clothing edges.',
    },
    {
      label: 'Skin smoothing',
      type: 'skin_smoothing',
      description: 'Soften camera noise while preserving realistic texture.',
    },
    {
      label: 'Wrinkle softening',
      type: 'wrinkle_softening',
      description: 'Subtly soften harsh crease shadows.',
    },
  ];
  exportOptions: PlatformExportOption[] = [];
  selectedExportCodes = new Set<string>([
    'linkedin_profile',
    'google_avatar',
    'resume_headshot',
    'original_high_res',
  ]);
  isDownloadingPackage = false;
  exportSuccessMessage = '';
  isApplyingPremiumAugmentation = false;
  adjustmentZoom = 100;
  adjustmentRotate = 0;
  adjustmentBrightness = 100;
  adjustmentContrast = 100;

  // Save to workspace state
  isSaving = false;
  saveSuccessMessage = '';
  isSaved = false;

  private _stateSubscription!: Subscription;
  private _consentSubscription?: Subscription;
  private _selectedFileToken = 0;
  private _nextRequestIsRegeneration = false;
  private _activeGenerationClientRequestId: string | null = null;
  private readonly _interruptedGenerationKey = 'photoWorkspaceInterruptedGeneration';

  constructor(
    private _replicateService: ReplicateService,
    private _fileUploadService: FileUploadService,
    private _headshotGenerationService: HeadshotGenerationService,
    private _stateService: WorkspaceStateService,
    private _creditService: CreditService,
    private _configService: ConfigService,
    private _authService: AuthService,
    private _biometricConsentService: BiometricConsentService,
    private _profileWorkflowService: ProfileWorkflowService,
    private _styleService: StyleService,
    private _stylePreviewService: StylePreviewService,
    private _analytics: AnalyticsService,
    private _route: ActivatedRoute,
    private _router: Router,
    private _cdr: ChangeDetectorRef
  ) {
    this.turnstileSiteKey = this._configService.turnstileSiteKey;
    this.isHeadshotMvpEnabled = this._configService.isOpenAIHeadshotMvpEnabled;
    this.isProfileWorkflowEnabled = this._configService.isProfilePhotoWorkflowOverhaulEnabled;
    this.areOutcomePackagesVisible = this._configService.areOutcomePackagesVisible;
    this.isProfilePhotoScoreVisible = this._configService.isProfilePhotoScoreVisible;
    this.isCreativeStylePackVisible = this._configService.isCreativeStylePackVisible;
    this.arePremiumAugmentationsVisible = this._configService.arePremiumAugmentationsVisible;
    if (this.isHeadshotMvpEnabled) {
      this.enhancementType = 'headshot';
    }
  }

  // Get total available credits from internal sources only
  getTotalAvailableCredits(): number {
    return this._creditService.getTotalAvailableCredits(
      this.userCreditStatus,
      null // No Replicate credits
    );
  }

  // Get required credits based on selected flow
  getRequiredCredits(): number {
    return this.isHeadshotMvpEnabled && this.enhancementType === 'headshot'
      ? this._creditService.getCreditCostSync('instant_headshot_generation')
      : this._creditService.getCreditCostSync('photo_enhancement');
  }

  // Check whether the selected operation has its own package allowance.
  hasEnoughCredits(isRegeneration = false): boolean {
    const usesPackageRefinement = [
      'headshot_linkedin',
      'headshot_creator',
      'headshot_office',
      'headshot_studio',
    ].includes(this.enhancementType);
    if (
      this.isHeadshotMvpEnabled &&
      (this.enhancementType === 'headshot' || usesPackageRefinement)
    ) {
      return this.selectedPackageCode === 'free_preview'
        ? !usesPackageRefinement && !isRegeneration
        : this.hasSelectedPackageEntitlement(isRegeneration || usesPackageRefinement);
    }

    const totalCredits = this.getTotalAvailableCredits();
    const requiredCredits = this.getRequiredCredits();
    return totalCredits >= requiredCredits;
  }

  hasSelectedPackageEntitlement(isRegeneration = false): boolean {
    if (this.selectedPackageCode === 'free_preview') {
      return !isRegeneration;
    }

    return this.hasPackageEntitlementForGeneration(this.selectedPackageCode, isRegeneration);
  }

  private hasPackageEntitlementForGeneration(packageCode: string, isRegeneration = false): boolean {
    const requiredCandidates =
      packageCode === this.selectedPackageCode
        ? isRegeneration
          ? 1
          : this.getCandidateRequestCountForSelectedPackage()
        : this.getRemainingCandidateCount(packageCode);
    return this.packageEntitlements.some(
      entitlement =>
        entitlement.packageCode === packageCode &&
        entitlement.status.toLowerCase() === 'active' &&
        (isRegeneration
          ? entitlement.remainingRefinements > 0
          : entitlement.remainingPackageUses > 0 &&
            entitlement.remainingCandidates >= requiredCandidates)
    );
  }

  hasPremiumAugmentationEntitlement(): boolean {
    return this.getRemainingPremiumAugmentationCount() > 0;
  }

  getRemainingPremiumAugmentationCount(): number {
    return this.packageEntitlements
      .filter(entitlement => entitlement.status.toLowerCase() === 'active')
      .reduce(
        (total, entitlement) => total + Math.max(0, entitlement.remainingPremiumAugmentations || 0),
        0
      );
  }

  isPremiumAddOnUnlockedForCurrentCandidate(): boolean {
    return this.selectedPackageCode !== 'free_preview' && this.hasPremiumAugmentationEntitlement();
  }

  getPremiumAddOnStatusText(): string {
    if (this.selectedPackageCode === 'free_preview') {
      return 'Premium add-ons require Pro and apply to unwatermarked paid candidates.';
    }

    if (this.selectedPackageCode === 'starter_package') {
      return 'Premium add-ons are a Pro feature. Starter includes candidates, refinements, and exports.';
    }

    const remaining = this.getRemainingPremiumAugmentationCount();
    if (remaining > 0) {
      return `${remaining} Pro premium add-on${remaining === 1 ? '' : 's'} available. Candidate generation does not use this allowance.`;
    }

    if (this.hasActiveProEntitlement()) {
      return 'No Pro premium add-ons remain. Candidate generation uses candidate allowance; add-ons use a separate Pro allowance.';
    }

    return 'Unlock Pro to apply premium add-ons.';
  }

  getPremiumAddOnBadgeText(): string {
    if (this.isPremiumAddOnUnlockedForCurrentCandidate()) {
      return 'Available now';
    }

    if (this.selectedPackageCode === 'starter_package') {
      return 'Requires Pro';
    }

    return this.hasActiveProEntitlement() ? 'No Pro add-ons remaining' : 'Requires Pro';
  }

  private hasActiveProEntitlement(): boolean {
    return this.packageEntitlements.some(
      entitlement =>
        entitlement.packageCode === 'pro_package' && entitlement.status.toLowerCase() === 'active'
    );
  }

  hasActivePaidEntitlement(): boolean {
    return this.packageEntitlements.some(
      entitlement =>
        entitlement.status.toLowerCase() === 'active' &&
        (entitlement.packageCode === 'starter_package' ||
          entitlement.packageCode === 'pro_package') &&
        (entitlement.remainingPackageUses > 0 ||
          entitlement.remainingRefinements > 0 ||
          entitlement.remainingPremiumAugmentations > 0)
    );
  }

  private getBestActivePaidPackageCode(): 'starter_package' | 'pro_package' | null {
    const activeForGeneration = (packageCode: 'starter_package' | 'pro_package') =>
      this.packageEntitlements.some(
        entitlement =>
          entitlement.packageCode === packageCode &&
          entitlement.status.toLowerCase() === 'active' &&
          entitlement.remainingPackageUses > 0 &&
          entitlement.remainingCandidates > 0
      );

    if (activeForGeneration('pro_package')) {
      return 'pro_package';
    }

    return activeForGeneration('starter_package') ? 'starter_package' : null;
  }

  canApplyPremiumAugmentation(): boolean {
    return (
      this.arePremiumAugmentationsVisible &&
      !this.isProcessing &&
      !this.isApplyingPremiumAugmentation &&
      !!this.enhancedImage &&
      this.isCandidateFulfillmentComplete() &&
      this.isPremiumAddOnUnlockedForCurrentCandidate()
    );
  }

  getSelectedPackageLabel(): string {
    return this.getPackageLabel(this.selectedPackageCode);
  }

  getPackageLabel(packageCode: string): string {
    return this.packageOptions.find(option => option.code === packageCode)?.name ?? 'Free Preview';
  }

  getPaidUpgradeOptions(): OutcomePackageDefinition[] {
    if (
      this.getBestActivePaidPackageCode() ||
      (this.previewCandidate && !this.rawPreviewAvailable) ||
      (this.resumablePreview && !this.resumablePreview.hasRawPreview)
    ) {
      return [];
    }

    return this.packageOptions.filter(
      option => option.code === 'starter_package' || option.code === 'pro_package'
    );
  }

  getRemainingCandidateCount(packageCode: string): number {
    return this.getFreePreviewPromotionPlan(packageCode).remainingCandidateCount;
  }

  canPromotePreviewCandidate(packageCode: string = this.selectedPackageCode): boolean {
    return this.getFreePreviewPromotionPlan(packageCode).canPromotePreview;
  }

  getUpgradeContinuityMessage(packageCode: string): string {
    return this.getFreePreviewPromotionPlan(packageCode).continuityMessage;
  }

  private getFreePreviewPromotionPlan(packageCode: string) {
    const totalCandidateCount =
      this.packageOptions.find(option => option.code === packageCode)?.includedCandidateCount ?? 1;
    return this.freePreviewPromotion.plan({
      packageCode,
      totalCandidateCount,
      hasPreviewCandidate: !!this.previewCandidate,
    });
  }

  isPackageOptionAvailable(packageCode: string): boolean {
    if (packageCode === 'free_preview') {
      return true;
    }

    return this.packageEntitlements.some(
      entitlement =>
        entitlement.packageCode === packageCode &&
        entitlement.status.toLowerCase() === 'active' &&
        entitlement.remainingPackageUses > 0 &&
        entitlement.remainingCandidates > 0
    );
  }

  getSelectedPackageStatus(): string {
    if (this.selectedPackageCode === 'free_preview') {
      return 'Free Preview is active: one watermarked preview candidate. Upgrade to unlock paid candidates, add-ons, and exports.';
    }

    const entitlement = this.packageEntitlements.find(
      item =>
        item.packageCode === this.selectedPackageCode && item.status.toLowerCase() === 'active'
    );
    if (!entitlement) {
      return `${this.getSelectedPackageLabel()} is locked until purchase grants an entitlement.`;
    }

    return `${this.getSelectedPackageLabel()} active: ${entitlement.remainingCandidates} candidates, ${entitlement.remainingRefinements} refinements, ${entitlement.remainingPremiumAugmentations} premium add-ons, export kit ${entitlement.platformExportKitAvailable ? 'available' : 'used'}.`;
  }

  getPackageStateLabel(): string {
    return this.selectedPackageCode === 'free_preview'
      ? 'Free Preview'
      : `${this.getSelectedPackageLabel()} active`;
  }

  getGeneratedCandidateCount(): number {
    const total = this.getSelectedCandidateCount();
    const entitlement = this.getCurrentPackageEntitlement();
    const consumed = entitlement ? total - Math.max(entitlement.remainingCandidates, 0) : 0;
    return Math.min(Math.max(this.generatedCandidates.length, consumed), total);
  }

  getRemainingCandidateSlots(): number {
    return Math.max(this.getSelectedCandidateCount() - this.getGeneratedCandidateCount(), 0);
  }

  isCandidateFulfillmentComplete(): boolean {
    return this.getRemainingCandidateSlots() === 0 && this.getGeneratedCandidateCount() > 0;
  }

  isPaidPackageFulfillmentPending(): boolean {
    return this.selectedPackageCode !== 'free_preview' && this.getRemainingCandidateSlots() > 0;
  }

  canShowFinishingTools(): boolean {
    return this.selectedPackageCode === 'free_preview' || this.isCandidateFulfillmentComplete();
  }

  canDownloadPackage(): boolean {
    return !!(
      this.selectedPackageCode !== 'free_preview' &&
      this.isCandidateFulfillmentComplete() &&
      !this.isLoadingEntitlements &&
      this.getCurrentPackageEntitlement()?.platformExportKitAvailable
    );
  }

  getPackageProgressText(): string {
    return `${this.getGeneratedCandidateCount()} of ${this.getSelectedCandidateCount()} generated`;
  }

  getCurrentPackageEntitlement(): PackageEntitlement | null {
    return (
      this.packageEntitlements.find(
        entitlement =>
          entitlement.packageCode === this.selectedPackageCode &&
          entitlement.status.toLowerCase() === 'active'
      ) ?? null
    );
  }

  getFulfillmentBlockerText(): string {
    if (this.isLoadingEntitlements) {
      return 'Checking your package allowance…';
    }

    const entitlement = this.getCurrentPackageEntitlement();
    if (!entitlement) {
      return 'We could not find an active package. Refresh once, then contact support if your purchase is still missing.';
    }

    if (
      entitlement.remainingPackageUses <= 0 ||
      entitlement.remainingCandidates < this.getRemainingCandidateSlots()
    ) {
      return 'Your candidate allowance does not match the unfinished package. Contact support without using a refinement.';
    }

    return this.getDisabledCtaLabel();
  }

  getCandidateAccessibleLabel(candidate: CandidateViewModel, index: number): string {
    const score = candidate.recommendationScore ?? candidate.score?.overallScore;
    const recommendation =
      index === 0 && candidate.recommendationScore ? ', recommended best shot' : '';
    return `Candidate ${index + 1}${recommendation}${score ? `, score ${score} out of 100` : ''}`;
  }

  isCandidateSelected(candidate: CandidateViewModel): boolean {
    return this.selectedCandidateId === candidate.processedImageId;
  }

  canStartRegeneration(): boolean {
    return (
      this.selectedPackageCode !== 'free_preview' &&
      !!this.enhancedImage &&
      this.isCandidateFulfillmentComplete() &&
      this.canStartEnhancement(true)
    );
  }

  requiresTurnstile(): boolean {
    return !!this.turnstileSiteKey;
  }

  canStartEnhancement(isRegeneration = false): boolean {
    return (
      !this.isLoadingAccountStatus &&
      this.isEmailConfirmed &&
      !this.isProcessing &&
      this.hasEnhancementSourceReady() &&
      this.hasEnoughCredits(isRegeneration) &&
      (!this.isHeadshotMvpEnabled || !!this.selectedPortraitStyle) &&
      (!this.isHeadshotMvpEnabled || isRegeneration || this.getRemainingCandidateSlots() > 0) &&
      (!this.requiresTurnstile() || !!this.turnstileToken) &&
      this.biometricConsentAccepted &&
      !this.isQualityGateBlockingGeneration()
    );
  }

  private hasEnhancementSourceReady(): boolean {
    return !!this.selectedFile || this.canUseStoredPreviewSource();
  }

  private canUseStoredPreviewSource(): boolean {
    return !!(this.isHeadshotMvpEnabled && !this.selectedFile && this.previewSourceStoragePath);
  }

  isQualityGateBlockingGeneration(): boolean {
    if (!this.isHeadshotMvpEnabled || !this.profileScore?.qualityGate) {
      return false;
    }

    return (
      this.profileScore.qualityGate.status === 'blocked' ||
      (this.profileScore.qualityGate.status === 'warning' && !this.qualityGateOverrideAccepted)
    );
  }

  acceptQualityGateWarning(): void {
    this.qualityGateOverrideAccepted = true;
    this._cdr.markForCheck();
  }

  getPrimaryCtaLabel(): string {
    if (!this.isHeadshotMvpEnabled) {
      return 'Transform Photo';
    }

    if (this.selectedPackageCode === 'free_preview') {
      return 'Generate Free Preview';
    }

    const remaining = this.getRemainingCandidateSlots();
    const noun = remaining === 1 ? 'photo' : 'photos';
    return this.getGeneratedCandidateCount() > 0
      ? `Generate remaining ${remaining} ${noun}`
      : `Generate ${remaining} ${noun}`;
  }

  getDisabledCtaLabel(): string {
    if (!this.isEmailConfirmed) {
      return 'Verify Email to Continue';
    }

    if (this.isHeadshotMvpEnabled && !this.selectedPortraitStyle) {
      return 'Choose a Portrait Style';
    }

    if (!this.biometricConsentAccepted) {
      return 'Accept Consent to Continue';
    }

    if (this.requiresTurnstile() && !this.turnstileToken) {
      return 'Complete Bot Check';
    }

    if (!this.hasEnhancementSourceReady()) {
      return this.isHeadshotMvpEnabled ? 'Upload or Resume a Preview' : 'Upload a Photo';
    }

    if (this.isQualityGateBlockingGeneration()) {
      return this.profileScore?.qualityGate?.status === 'blocked'
        ? 'Upload a Better Photo'
        : 'Review Quality Warning';
    }

    if (this.isHeadshotMvpEnabled && this.getRemainingCandidateSlots() === 0) {
      return 'Candidate set complete';
    }

    if (!this.hasEnoughCredits()) {
      return this.isHeadshotMvpEnabled ? 'Package Entitlement Needed' : 'Package Needed';
    }

    if (this.isProcessing) {
      return this.processingStatus || 'Preparing…';
    }

    return 'Preparing…';
  }

  canResumeInterruptedGeneration(): boolean {
    return (
      !!this.interruptedGeneration &&
      this.canStartEnhancement(this.interruptedGeneration.isRegeneration)
    );
  }

  resumeInterruptedGeneration(): void {
    const draft = this.interruptedGeneration;
    if (!draft) {
      return;
    }

    this.selectedPackageCode = draft.packageCode;
    this.selectedUseCaseCode = draft.useCaseCode;
    this.previewSourceStoragePath = draft.imageStoragePath;
    this.currentSourceStoragePath = draft.imageStoragePath;
    this.pendingPaidStyleName = draft.styleName;
    this.selectPortraitStyleByName(draft.styleName);
    this._activeGenerationClientRequestId = draft.clientRequestId;
    this._nextRequestIsRegeneration = draft.isRegeneration;
    if (!this.canStartEnhancement(draft.isRegeneration)) {
      this.errorMessage = draft.isRegeneration
        ? 'Complete the requirement shown above before resuming this refinement.'
        : this.getDisabledCtaLabel();
      this._cdr.markForCheck();
      return;
    }

    this._headshotGenerationService.getResumablePreview().subscribe({
      next: response => {
        const preview = response.success ? response.data : null;
        if (!preview?.hasRawPreview) {
          this.resumablePreview = preview;
          this._cdr.markForCheck();
          return;
        }

        void this.resumePreview(preview).then(() => {
          if (this.canStartEnhancement(draft.isRegeneration)) {
            void this.startEnhancement();
          }
        });
      },
      error: () => {
        // Keep the persisted draft visible so the user can retry when the workspace is reachable.
        this._cdr.markForCheck();
      },
    });
  }

  discardInterruptedGeneration(): void {
    this.clearInterruptedGeneration();
    this.enhanceAnother();
    this._cdr.markForCheck();
  }

  private restoreInterruptedGeneration(): void {
    try {
      const raw = localStorage.getItem(this._interruptedGenerationKey);
      if (!raw) {
        return;
      }
      const draft = JSON.parse(raw) as InterruptedGenerationDraft;
      const age = Date.now() - Date.parse(draft.startedAt);
      if (
        !draft.clientRequestId ||
        !draft.imageStoragePath ||
        !draft.styleName ||
        !Number.isFinite(age) ||
        age > 24 * 60 * 60 * 1000
      ) {
        localStorage.removeItem(this._interruptedGenerationKey);
        return;
      }

      this.interruptedGeneration = draft;
      this._activeGenerationClientRequestId = draft.clientRequestId;
      this.previewSourceStoragePath = draft.imageStoragePath;
      this.currentSourceStoragePath = draft.imageStoragePath;
      this.pendingPaidStyleName = draft.styleName;
      const sourceState = this.photoWorkspaceSession.createStoredPreviewSourceState(
        draft.imageStoragePath,
        this.getStorageProxyUrl(draft.imageStoragePath)
      );
      this.imagePreview = sourceState.imagePreview;
      this.beforeImageLoadFailed = sourceState.beforeImageLoadFailed;
    } catch {
      localStorage.removeItem(this._interruptedGenerationKey);
    }
  }

  private persistInterruptedGeneration(draft: InterruptedGenerationDraft): void {
    this.interruptedGeneration = draft;
    this._activeGenerationClientRequestId = draft.clientRequestId;
    localStorage.setItem(this._interruptedGenerationKey, JSON.stringify(draft));
  }

  private clearInterruptedGeneration(): void {
    this.interruptedGeneration = null;
    this._activeGenerationClientRequestId = null;
    localStorage.removeItem(this._interruptedGenerationKey);
  }

  private createGenerationClientRequestId(): string {
    return typeof crypto !== 'undefined' && 'randomUUID' in crypto
      ? crypto.randomUUID()
      : `headshot-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
  }

  ngOnInit() {
    this.restoreInterruptedGeneration();
    this.loadAccountStatus();

    // Load user credit status
    const currentState = this._stateService.getState();

    if (!currentState.userCreditStatus) {
      this.isLoadingCredits = true;
      this._stateService.loadCreditsOnly();
    } else {
      this.isLoadingCredits = false;
      this.userCreditStatus = currentState.userCreditStatus;
    }

    this._stateSubscription = this._stateService.state$.subscribe(state => {
      this.userCreditStatus = state.userCreditStatus;
      this.isLoadingCredits = state.isLoading;
      this._cdr.detectChanges();
    });

    this._consentSubscription = this._biometricConsentService.consent$.subscribe(consent => {
      this.biometricConsentAccepted = !!consent?.accepted;
      this._cdr.markForCheck();
    });

    if (this.isHeadshotMvpEnabled) {
      this.loadPortraitStyles();
    }

    if (this.areOutcomePackagesVisible) {
      this.loadOutcomePackages();
      this.loadPackageEntitlements();
    }

    this._route.queryParamMap.subscribe(params => {
      const upgraded = params.get('upgraded');
      const resumePreviewId = Number(params.get('resumePreviewId') ?? params.get('previewId'));
      const refineImageId = Number(params.get('refineImageId'));
      this.applyUseCaseFromQuery(params.get('useCase'));
      if (Number.isFinite(refineImageId) && refineImageId > 0) {
        this.loadGalleryImageForRefinement(refineImageId);
        return;
      }
      if (upgraded === 'starter_package' || upgraded === 'pro_package') {
        this.restorePreviewDraft();
        this.selectedPackageCode = upgraded;
        this.trackVerticalFunnelEvent('vertical_pack_purchase_success', { packageCode: upgraded });
        this.loadPackageEntitlements();
        this.loadResumablePreview(
          Number.isFinite(resumePreviewId) && resumePreviewId > 0 ? resumePreviewId : undefined,
          true
        );
        this.saveSuccessMessage = `${this.getPackageLabel(upgraded)} unlocked. Your preview is ready; generate the remaining candidates when ready.`;
        this._cdr.markForCheck();
      } else {
        sessionStorage.removeItem('enhanceUpgradeDraft');
        if (Number.isFinite(resumePreviewId) && resumePreviewId > 0) {
          this.loadResumablePreview(resumePreviewId, true);
        } else if (this.isHeadshotMvpEnabled) {
          this.loadResumablePreview();
        }
      }
    });

    if (!this.isProfileWorkflowEnabled) {
      return;
    }

    this._profileWorkflowService.getExportOptions().subscribe({
      next: response => {
        if (response.success) {
          this.exportOptions = response.data;
          this._cdr.markForCheck();
        }
      },
      error: error => console.warn('Failed to load platform export options', error),
    });
  }

  private loadGalleryImageForRefinement(processedImageId: number): void {
    this._profileWorkflowService.getStudioImageSource(processedImageId).subscribe({
      next: response => {
        if (!response.success || !response.data) {
          this.errorMessage =
            response.error?.message ?? 'This photo is no longer available to refine.';
          this._cdr.markForCheck();
          return;
        }

        const source = response.data;
        this.selectedFile = null;
        this.previewSourceStoragePath = source.storagePath;
        this.currentSourceStoragePath = source.storagePath;
        this.imagePreview = source.imageUrl;
        this.beforeImageLoadFailed = false;
        this.rawPreviewAvailable = false;
        this.previewCandidate = null;
        this.generatedCandidates = [];
        this.selectedCandidateId = source.processedImageId;
        this.enhancedImage = this.createEnhancedImageViewModel(
          source.imageUrl,
          'enhanced',
          source.processedImageId,
          source.storagePath
        );
        this.enhancementType = 'headshot_linkedin';
        this.selectedPackageCode = this.getBestActivePaidPackageCode() ?? this.selectedPackageCode;
        this.saveSuccessMessage =
          'Photo loaded from your workspace. Choose a refinement to continue.';
        this._cdr.markForCheck();
      },
      error: () => {
        this.errorMessage = 'This photo is no longer available to refine.';
        this._cdr.markForCheck();
      },
    });
  }

  private restorePreviewDraft(): void {
    try {
      const raw = sessionStorage.getItem('enhanceUpgradeDraft');
      if (!raw) {
        return;
      }

      const draft = JSON.parse(raw) as {
        styleName?: string;
        paidStyleName?: string;
        sourceStoragePath?: string;
        useCaseCode?: PackUseCaseCode;
        candidate?: CandidateViewModel;
      };
      if (!draft.candidate || !draft.sourceStoragePath) {
        return;
      }

      if (draft.useCaseCode) {
        this.selectedUseCaseCode = this.normalizeUseCaseCode(draft.useCaseCode);
        this.applyDefaultExportsForUseCase();
      }
      this.previewCandidate = draft.candidate;
      this.previewStyleName = draft.styleName ?? null;
      const sourceState = this.photoWorkspaceSession.createStoredPreviewSourceState(
        draft.sourceStoragePath,
        this.getStorageProxyUrl(draft.sourceStoragePath)
      );
      this.previewSourceStoragePath = sourceState.previewSourceStoragePath;
      this.currentSourceStoragePath = sourceState.currentSourceStoragePath;
      this.imagePreview = sourceState.imagePreview;
      this.beforeImageLoadFailed = sourceState.beforeImageLoadFailed;
      this.generatedCandidates = [{ ...draft.candidate, promotedFromPreview: true }];
      this.selectCandidate(this.generatedCandidates[0]);
      this.pendingPaidStyleName = draft.paidStyleName ?? null;
    } catch (error) {
      console.warn('Failed to restore enhance upgrade draft', error);
    }
  }

  private applyUseCaseFromQuery(useCase: string | null): void {
    const normalized = this.normalizeUseCaseCode(useCase);
    if (normalized !== this.selectedUseCaseCode) {
      this.selectedUseCaseCode = normalized;
      this.trackVerticalFunnelEvent('vertical_pack_page_view');
      this.applyDefaultExportsForUseCase();
      this.selectRecommendedStyleForUseCase();
    }
  }

  selectUseCase(useCase: PackUseCaseCode): void {
    this.selectedUseCaseCode = useCase;
    this.trackVerticalFunnelEvent('vertical_pack_use_case_selected');
    this.applyDefaultExportsForUseCase();
    this.selectRecommendedStyleForUseCase();
    this._cdr.markForCheck();
  }

  getSelectedUseCase(): PackUseCaseOption {
    return (
      this.packUseCases.find(option => option.code === this.selectedUseCaseCode) ??
      this.packUseCases[0]
    );
  }

  private normalizeUseCaseCode(useCase: string | null | undefined): PackUseCaseCode {
    const normalized = (useCase ?? '').trim().toLowerCase().replace(/-/g, '_');
    return this.packUseCases.some(option => option.code === normalized)
      ? (normalized as PackUseCaseCode)
      : 'linkedin_executive';
  }

  private applyDefaultExportsForUseCase(): void {
    this.selectedExportCodes = new Set(this.getSelectedUseCase().defaultExports);
  }

  private selectRecommendedStyleForUseCase(): void {
    if (!this.portraitStyles.length) {
      return;
    }
    const useCase = this.getSelectedUseCase();
    const match = this.portraitStyleCatalog.selectRecommendedForUseCase(
      this.portraitStyles,
      useCase
    );
    if (match) {
      this.selectedPortraitStyle = match;
      this.selectedStyleGroup = match.group;
    }
  }

  private trackVerticalFunnelEvent(eventName: string, extra: Record<string, unknown> = {}): void {
    this._analytics.trackEvent(eventName, {
      useCaseCode: this.selectedUseCaseCode,
      packageCode: this.selectedPackageCode,
      ...extra,
    });
  }

  private loadResumablePreview(previewId?: number, autoResume = false): void {
    this.isLoadingResumablePreview = true;
    this._headshotGenerationService.getResumablePreview(previewId).subscribe({
      next: response => {
        this.isLoadingResumablePreview = false;
        this.resumablePreview = response.success ? response.data : null;
        if (this.resumablePreview?.candidates?.length) {
          this.clearInterruptedGeneration();
        }
        const hasPaidCandidates =
          !!this.resumablePreview?.candidates?.length &&
          (this.resumablePreview.activePackageCode === 'starter_package' ||
            this.resumablePreview.activePackageCode === 'pro_package');
        const shouldAutoResume =
          hasPaidCandidates || (this.resumablePreview?.hasRawPreview && autoResume);
        if (shouldAutoResume && this.resumablePreview) {
          this.resumePreview(this.resumablePreview);
        }
        this._cdr.markForCheck();
      },
      error: error => {
        this.isLoadingResumablePreview = false;
        console.warn('Failed to load resumable preview', error);
        this._cdr.markForCheck();
      },
    });
  }

  async resumePreview(preview: ResumableHeadshotPreview): Promise<void> {
    const promotedCandidate = preview.promotedCandidate;
    const candidate: CandidateViewModel = {
      imageUrl: promotedCandidate?.imageUrl ?? preview.imageUrl,
      storagePath: promotedCandidate?.storagePath ?? preview.storagePath,
      processedImageId: promotedCandidate?.processedImageId ?? preview.processedImageId,
      provider: promotedCandidate?.provider ?? 'openai',
      model: promotedCandidate?.model ?? 'gpt-image-2',
      correlationId: promotedCandidate?.correlationId ?? '',
      promotedFromPreview: !!promotedCandidate,
    };
    const resumedCandidates = await this.loadAuthorizedCandidateImages(
      preview.candidates?.length ? preview.candidates.map(item => ({ ...item })) : [candidate]
    );
    this.previewCandidate = resumedCandidates[0];
    this.resumablePreview = null;
    this.previewStyleName = preview.style;
    const sourceState = this.photoWorkspaceSession.createStoredPreviewSourceState(
      preview.sourceAvailable === false ? null : preview.sourceStoragePath,
      preview.sourceAvailable === false ? null : this.getStorageProxyUrl(preview.sourceStoragePath)
    );
    this.previewSourceStoragePath = sourceState.previewSourceStoragePath;
    this.currentSourceStoragePath = sourceState.currentSourceStoragePath;
    this.rawPreviewAvailable = preview.hasRawPreview;
    this.imagePreview = sourceState.imagePreview;
    this.beforeImageLoadFailed = sourceState.beforeImageLoadFailed;
    this.generatedCandidates = resumedCandidates;
    this.selectCandidate(resumedCandidates[0]);
    this.clearInterruptedGeneration();
    this.selectPortraitStyleByName(preview.style);
    if (this.isProfilePhotoScoreVisible && resumedCandidates.length > 1) {
      this.scoreAllCandidates();
    }
    if (
      preview.activePackageCode === 'starter_package' ||
      preview.activePackageCode === 'pro_package'
    ) {
      this.selectedPackageCode = preview.activePackageCode;
      this.saveSuccessMessage =
        preview.sourceAvailable === false
          ? 'Your saved candidates are restored, but the original upload expired. Upload a new photo set to generate more.'
          : `${this.getPackageLabel(preview.activePackageCode)} is unlocked. Your preview is ready; generate the remaining candidates when ready.`;
    } else {
      this.selectedPackageCode = 'free_preview';
      this.saveSuccessMessage =
        'Preview restored. Unlock Starter or Pro to continue from this result.';
    }
    this._cdr.markForCheck();
  }

  startOverFromResumablePreview(): void {
    this.resumablePreview = null;
    this.removeFile();
  }

  private selectPortraitStyleByName(styleName: string | null | undefined): void {
    if (!styleName) {
      return;
    }
    const match = this.portraitStyleCatalog.findByStyleName(this.portraitStyles, styleName);
    if (match) {
      this.selectedPortraitStyle = match;
      this.selectedStyleGroup = match.group;
    }
  }

  private loadPortraitStyles(): void {
    this.isLoadingPortraitStyles = true;
    this.portraitStyleError = '';
    this._styleService.getActiveStyles().subscribe({
      next: response => {
        if (response.success && response.data?.length) {
          this.portraitStyles = response.data
            .map(style => this.toPortraitStyleCard(style))
            .sort((a, b) => a.displayOrder - b.displayOrder || a.name.localeCompare(b.name));
          this.selectedPortraitStyle = this.portraitStyleCatalog.selectInitialStyle(
            this.portraitStyles,
            [this.pendingPaidStyleName, this.previewStyleName]
          );
          this.selectRecommendedStyleForUseCase();
        } else {
          this.portraitStyleError = 'Portrait styles are unavailable right now.';
          this.portraitStyles = [];
          this.selectedPortraitStyle = null;
        }
        this.isLoadingPortraitStyles = false;
        this._cdr.markForCheck();
      },
      error: error => {
        console.warn('Failed to load portrait styles', error);
        this.portraitStyleError = 'Portrait styles are unavailable right now.';
        this.isLoadingPortraitStyles = false;
        this._cdr.markForCheck();
      },
    });
  }

  private toPortraitStyleCard(style: Style): PortraitStyleCard {
    const key = this.normalizeStyleKey(style.name);
    const metadata = this.getPortraitStyleMetadata(key);
    return {
      style,
      key,
      name: this.toDisplayStyleName(style.name),
      description: metadata.description || style.description,
      previewUrl: this._stylePreviewService.getCachedUrl(style.name),
      group: metadata.group,
      badgeLabel: metadata.badgeLabel,
      displayOrder: metadata.displayOrder,
    };
  }

  private normalizeStyleKey(name: string): string {
    return name
      .trim()
      .toLowerCase()
      .replace(/[\s/_]+/g, '-');
  }

  private toDisplayStyleName(name: string): string {
    return name
      .split(/[\s-_]+/)
      .filter(Boolean)
      .map(part => part.charAt(0).toUpperCase() + part.slice(1))
      .join(' ');
  }

  private getPortraitStyleMetadata(
    key: string
  ): Pick<PortraitStyleCard, 'group' | 'badgeLabel' | 'displayOrder' | 'description'> {
    const metadata: Record<
      string,
      Pick<PortraitStyleCard, 'group' | 'badgeLabel' | 'displayOrder' | 'description'>
    > = {
      linkedin: {
        group: 'recommended',
        badgeLabel: 'General',
        displayOrder: 10,
        description: 'Balanced professional look for broad career use.',
      },
      executive: {
        group: 'recommended',
        badgeLabel: 'Leadership',
        displayOrder: 20,
        description: 'Boardroom-ready polish for founders and leaders.',
      },
      entrepreneur: {
        group: 'recommended',
        badgeLabel: 'Founder',
        displayOrder: 30,
        description: 'Confident, modern profile for builders and owners.',
      },
      startup: {
        group: 'recommended',
        badgeLabel: 'Startup',
        displayOrder: 40,
        description: 'Approachable, high-energy look for startup teams.',
      },
      'tech-professional': {
        group: 'recommended',
        badgeLabel: 'Tech',
        displayOrder: 50,
        description: 'Clean, modern profile for engineering and product roles.',
      },
      medical: {
        group: 'more',
        badgeLabel: 'Healthcare',
        displayOrder: 60,
        description: 'Calm, trusted presentation for clinical profiles.',
      },
      academic: {
        group: 'more',
        badgeLabel: 'Academic',
        displayOrder: 70,
        description: 'Credible portrait for educators, authors, and researchers.',
      },
      creative: {
        group: 'more',
        badgeLabel: 'Creative',
        displayOrder: 80,
        description: 'Professional polish with a more expressive feel.',
      },
      casual: {
        group: 'more',
        badgeLabel: 'Casual',
        displayOrder: 110,
        description: 'Relaxed but polished profile-photo style.',
      },
      fresh: {
        group: 'more',
        badgeLabel: 'Fresh',
        displayOrder: 120,
        description: 'Clean, bright, and approachable portrait style.',
      },
      artistic: {
        group: 'more',
        badgeLabel: 'Artistic',
        displayOrder: 130,
        description: 'Elevated portrait with a creative visual tone.',
      },
      'digital-native': {
        group: 'more',
        badgeLabel: 'Modern',
        displayOrder: 140,
        description: 'Contemporary profile look for digital-first platforms.',
      },
      'digital-nomad': {
        group: 'more',
        badgeLabel: 'Remote',
        displayOrder: 150,
        description: 'Warm, mobile-professional profile style.',
      },
      'edgy-urban': {
        group: 'more',
        badgeLabel: 'Urban',
        displayOrder: 160,
        description: 'Sharper city-inspired portrait style.',
      },
      fitness: {
        group: 'more',
        badgeLabel: 'Fitness',
        displayOrder: 170,
        description: 'Active, confident profile style.',
      },
      glamour: {
        group: 'more',
        badgeLabel: 'Polished',
        displayOrder: 180,
        description: 'More styled and high-polish portrait look.',
      },
      influencer: {
        group: 'more',
        badgeLabel: 'Social',
        displayOrder: 190,
        description: 'Creator-friendly portrait for public social profiles.',
      },
      'night-out': {
        group: 'more',
        badgeLabel: 'Social',
        displayOrder: 200,
        description: 'Stylized evening profile look.',
      },
      'beach-vibes': {
        group: 'more',
        badgeLabel: 'Lifestyle',
        displayOrder: 210,
        description: 'Bright lifestyle portrait for casual platforms.',
      },
      'retro-wave': {
        group: 'more',
        badgeLabel: 'Retro',
        displayOrder: 220,
        description: 'Stylized retro-inspired portrait.',
      },
      cartoon: {
        group: 'fun',
        badgeLabel: 'Avatar',
        displayOrder: 310,
        description: 'Friendly cartoon avatar for social use.',
      },
      chibi: {
        group: 'fun',
        badgeLabel: 'Avatar',
        displayOrder: 320,
        description: 'Cute anime-inspired avatar style.',
      },
      pixar: {
        group: 'fun',
        badgeLabel: 'Avatar',
        displayOrder: 330,
        description: 'Cinematic 3D avatar-inspired portrait.',
      },
      'pixar-3d': {
        group: 'fun',
        badgeLabel: 'Avatar',
        displayOrder: 330,
        description: 'Cinematic 3D avatar-inspired portrait.',
      },
    };
    return (
      metadata[key] ?? { group: 'more', badgeLabel: 'Portrait', displayOrder: 900, description: '' }
    );
  }

  getVisiblePortraitStyles(): PortraitStyleCard[] {
    const styles = this.portraitStyleCatalog.getVisibleStyles(
      this.portraitStyles,
      this.selectedStyleGroup
    );
    if (this.selectedStyleGroup !== 'recommended') {
      return styles;
    }

    const recommended = this.portraitStyleCatalog.getRecommendedStyles(
      this.portraitStyles,
      this.getSelectedUseCase()
    );
    return recommended;
  }

  hasPortraitStyleGroup(group: PortraitStyleGroup): boolean {
    return this.portraitStyleCatalog.hasGroup(this.portraitStyles, group);
  }

  selectPortraitStyle(style: PortraitStyleCard): void {
    this.selectedPortraitStyle = style;
    this.enhancementType = this.isHeadshotMvpEnabled ? 'headshot' : style.key;
    this._cdr.markForCheck();
  }

  setStyleGroup(group: PortraitStyleGroup): void {
    const selection = this.portraitStyleCatalog.selectGroup(
      this.portraitStyles,
      this.selectedPortraitStyle,
      group
    );
    if (!selection) {
      return;
    }

    this.selectedStyleGroup = selection.group;
    this.selectedPortraitStyle = selection.selected;
  }

  getStyleGroupLabel(group: PortraitStyleGroup): string {
    switch (group) {
      case 'recommended':
        return 'Recommended';
      case 'more':
        return 'More styles';
      case 'fun':
        return 'Fun';
    }
  }

  private loadOutcomePackages(): void {
    this.isLoadingPackages = true;
    this._profileWorkflowService.getOutcomePackages().subscribe({
      next: response => {
        if (response.success) {
          this.packageOptions = response.data;
          if (!this.packageOptions.some(option => option.code === this.selectedPackageCode)) {
            this.selectedPackageCode = 'free_preview';
          }
        }
        this.isLoadingPackages = false;
        this._cdr.markForCheck();
      },
      error: error => {
        console.warn('Failed to load outcome packages', error);
        this.isLoadingPackages = false;
        this._cdr.markForCheck();
      },
    });
  }

  private loadPackageEntitlements(): void {
    this.isLoadingEntitlements = true;
    this._profileWorkflowService.getEntitlements().subscribe({
      next: response => {
        this.isLoadingEntitlements = false;
        if (response.success) {
          this.packageEntitlements = response.data;
          this.applyActiveEntitlementSelection();
        }
        this._cdr.markForCheck();
      },
      error: error => {
        this.isLoadingEntitlements = false;
        console.warn('Failed to load package entitlements', error);
        this._cdr.markForCheck();
      },
    });
  }

  private applyActiveEntitlementSelection(): void {
    const activePackageCode = this.getBestActivePaidPackageCode();
    if (!activePackageCode) {
      return;
    }

    if (this.selectedPackageCode === 'free_preview') {
      this.selectedPackageCode = activePackageCode;
    }
  }

  ngOnDestroy() {
    if (this._stateSubscription) {
      this._stateSubscription.unsubscribe();
    }
    this._consentSubscription?.unsubscribe();
    this.revokeCandidateObjectUrls();
  }

  triggerFileUpload() {
    this.fileInput.nativeElement.click();
  }

  onFileSelected(event: any) {
    const file = event.target.files[0];
    if (file) {
      this.processFile(file);
    }
  }

  onDragOver(event: DragEvent) {
    event.preventDefault();
    this.isDragOver = true;
  }

  onDragLeave(event: DragEvent) {
    event.preventDefault();
    this.isDragOver = false;
  }

  onDrop(event: DragEvent) {
    event.preventDefault();
    this.isDragOver = false;

    const files = event.dataTransfer?.files;
    if (files && files.length > 0) {
      this.processFile(files[0]);
    }
  }

  processFile(file: File) {
    // Validate file type
    if (!this.allowedTypes.includes(file.type)) {
      this.errorMessage = 'Different format needed. Use JPEG, PNG, or WebP.';
      console.error('Invalid file type:', file.type);
      return;
    }

    if (file.size > 7 * 1024 * 1024) {
      this.errorMessage = 'File size must be less than 7MB.';
      console.error('File too large:', file.size);
      return;
    }

    this.selectedFile = file;
    this.errorMessage = '';
    this.profileScore = null;
    this.generatedScore = null;
    this.qualityGateOverrideAccepted = false;
    this.currentSourceStoragePath = null;
    this.rawPreviewAvailable = false;
    this.previewCandidate = null;
    this.previewSourceStoragePath = null;
    this.previewStyleName = null;
    sessionStorage.removeItem('enhanceUpgradeDraft');
    const fileToken = ++this._selectedFileToken;

    // Create preview
    const reader = new FileReader();
    reader.onload = e => {
      if (fileToken !== this._selectedFileToken || this.selectedFile !== file) {
        return;
      }

      Object.assign(
        this,
        this.photoWorkspaceSession.createLocalFilePreviewState(e.target?.result as string)
      );
      this._cdr.detectChanges();
      this.scoreSelectedPhoto(file, fileToken);
    };
    reader.onerror = e => {
      if (fileToken !== this._selectedFileToken || this.selectedFile !== file) {
        return;
      }

      console.error('FileReader error:', e);
      this.errorMessage = 'Failed to read the image file.';
      this._cdr.detectChanges();
    };
    reader.readAsDataURL(file);
  }

  private scoreSelectedPhoto(file: File, fileToken: number): void {
    if (
      !this.isProfilePhotoScoreVisible ||
      fileToken !== this._selectedFileToken ||
      this.selectedFile !== file
    ) {
      return;
    }

    this.isScoringPhoto = true;
    this._cdr.markForCheck();
    this._profileWorkflowService
      .scorePhoto(file)
      .pipe(
        finalize(() => {
          if (fileToken === this._selectedFileToken && this.selectedFile === file) {
            this.isScoringPhoto = false;
            this._cdr.markForCheck();
          }
        })
      )
      .subscribe({
        next: response => {
          if (
            response.success &&
            fileToken === this._selectedFileToken &&
            this.selectedFile === file
          ) {
            this.profileScore = response.data;
          }
        },
        error: error => {
          if (fileToken === this._selectedFileToken && this.selectedFile === file) {
            console.warn('Profile photo scoring failed', error);
          }
        },
      });
  }

  removeFile() {
    this._selectedFileToken++;
    this.selectedFile = null;
    Object.assign(this, this.photoWorkspaceSession.createClearedSourceState());
    this.rawPreviewAvailable = false;
    sessionStorage.removeItem('enhanceUpgradeDraft');
    // Trigger change detection to update the view
    this._cdr.detectChanges();
  }

  onTurnstileTokenChange(token: string): void {
    this.turnstileToken = token;
    this._cdr.markForCheck();
  }

  onBiometricConsentChange(accepted: boolean): void {
    if (accepted) {
      this._biometricConsentService.acceptConsent();
    } else {
      this._biometricConsentService.revokeConsent();
    }
    this._cdr.markForCheck();
  }

  dismissOrResetAfterError(): void {
    if (this.enhancedImage || this.selectedFile) {
      this.errorMessage = '';
      this._cdr.markForCheck();
      return;
    }

    this.resetComponent();
  }

  regenerateCurrentCandidate(): void {
    if (this.selectedPackageCode === 'free_preview') {
      this.errorMessage =
        'Regenerate is a premium feature. Upgrade to Starter or Pro to use refinements.';
      this._cdr.markForCheck();
      return;
    }

    this._nextRequestIsRegeneration = true;
    void this.startEnhancement();
  }

  // eslint-disable-next-line max-lines-per-function, complexity -- provider and package branches share one guarded transaction
  async startEnhancement() {
    if (!this.isEmailConfirmed) {
      this.verificationMessage =
        'Please verify your email address to use Photo Transform. Check your inbox (and spam) or resend verification.';
      this._cdr.detectChanges();
      return;
    }

    if (!this.biometricConsentAccepted) {
      this.errorMessage =
        'Please accept the biometric consent notice before transforming your photo.';
      this._cdr.detectChanges();
      return;
    }

    const canUseStoredPreviewSource = this.canUseStoredPreviewSource();
    if (!this.selectedFile && !canUseStoredPreviewSource) {
      return;
    }

    if (this.isQualityGateBlockingGeneration()) {
      this.errorMessage =
        this.profileScore?.qualityGate?.status === 'blocked'
          ? 'Upload a clearer single-person portrait before generating.'
          : 'Review the photo quality warning and choose Continue anyway before generating.';
      this._cdr.detectChanges();
      return;
    }

    if (!this.hasEnoughCredits(this._nextRequestIsRegeneration)) {
      this.errorMessage = this.isHeadshotMvpEnabled
        ? this._nextRequestIsRegeneration
          ? 'No refinements remain for this package.'
          : `Unlock or select an available ${this.getSelectedPackageLabel()} entitlement before generating candidates.`
        : 'Package needed before transforming this photo.';
      this._cdr.detectChanges();
      return;
    }

    if (this.turnstileSiteKey && !this.turnstileToken) {
      this.errorMessage = this.turnstile?.error || 'Complete the bot check above to continue.';
      this._cdr.detectChanges();
      return;
    }

    this.isProcessing = true;
    this.processingProgress = 0;
    this.processingStatus = 'Uploading image...';
    this.errorMessage = '';

    try {
      // Step 1: Upload the image file, or continue from a stored Free Preview source after upgrade.
      this.processingStatus = canUseStoredPreviewSource
        ? 'Preparing your package...'
        : 'Uploading image...';
      const uploadResult = canUseStoredPreviewSource
        ? { url: this.previewSourceStoragePath!, storagePath: this.previewSourceStoragePath! }
        : await this.uploadImageForEnhancement();

      if (!uploadResult?.url) {
        throw new Error('Failed to upload image');
      }
      this.currentSourceStoragePath = uploadResult.storagePath ?? null;

      // Step 2: Call the provider-agnostic instant headshot API when the MVP flag is enabled.
      this.processingProgress = 30;
      this.processingStatus = this.isHeadshotMvpEnabled
        ? 'Generating your professional headshot...'
        : 'Starting AI enhancement...';

      let finalResult;

      if (this.isHeadshotMvpEnabled && this.enhancementType === 'headshot') {
        if (!uploadResult.storagePath) {
          throw new Error('Uploaded image source was not returned by the server');
        }

        const candidateBeingRegeneratedId = this._nextRequestIsRegeneration
          ? this.selectedCandidateId
          : null;
        const promotedPreview =
          !this._nextRequestIsRegeneration && this.canPromotePreviewCandidate()
            ? this.previewCandidate
            : null;
        const styleName = this.selectedPortraitStyle?.style.name ?? 'linkedin';
        const requestCount = this._nextRequestIsRegeneration
          ? 1
          : this.getCandidateRequestCountForSelectedPackage();
        let resultCandidate: CandidateViewModel | null = null;

        // Keep every provider request below ingress/mobile connection limits. Each completed
        // candidate is persisted and merged before the next request, so a dropped connection
        // can resume the remaining work without duplicating fulfilled candidates.
        for (let requestIndex = 0; requestIndex < requestCount; requestIndex++) {
          const clientRequestId =
            requestIndex === 0
              ? (this._activeGenerationClientRequestId ?? this.createGenerationClientRequestId())
              : this.createGenerationClientRequestId();
          this.persistInterruptedGeneration({
            clientRequestId,
            imageStoragePath: uploadResult.storagePath,
            styleName,
            packageCode: this.selectedPackageCode,
            useCaseCode: this.selectedUseCaseCode,
            isRegeneration: this._nextRequestIsRegeneration,
            replacesProcessedImageId: candidateBeingRegeneratedId ?? undefined,
            startedAt: this.interruptedGeneration?.startedAt ?? new Date().toISOString(),
          });
          const headshotResponse = await firstValueFrom(
            this._headshotGenerationService.generateHeadshot({
              imageStoragePath: uploadResult.storagePath,
              style: styleName,
              background: 'auto',
              packageCode: this.selectedPackageCode,
              numOutputs: 1,
              isRegeneration: this._nextRequestIsRegeneration,
              reusedPreviewProcessedImageId: promotedPreview?.processedImageId,
              replacesProcessedImageId: candidateBeingRegeneratedId ?? undefined,
              useCaseCode: this.selectedUseCaseCode,
              clientRequestId,
              turnstileToken: this.turnstileSiteKey ? this.turnstileToken : undefined,
            })
          );

          if (!headshotResponse?.success || !headshotResponse.data?.imageUrl) {
            const errorMsg = headshotResponse?.error?.message || 'Headshot generation failed';
            console.error('Headshot API failed:', errorMsg);
            throw new Error(errorMsg);
          }

          const responseCandidates = await this.loadAuthorizedCandidateImages(
            this.toCandidateViewModels(headshotResponse.data)
          );
          resultCandidate = this._nextRequestIsRegeneration
            ? this.replaceRegeneratedCandidate(responseCandidates, candidateBeingRegeneratedId)
            : this.mergeGeneratedCandidates(responseCandidates);
          if (!resultCandidate) {
            throw new Error('Generated candidate was not returned');
          }
          this.selectedCandidateId = resultCandidate.processedImageId;
          this.processingProgress = 30 + Math.round(((requestIndex + 1) / requestCount) * 45);
          this.processingStatus =
            requestIndex + 1 < requestCount
              ? `Generated ${requestIndex + 1} of ${requestCount} candidates...`
              : 'Preparing your headshot...';
          this._cdr.detectChanges();
        }

        this.clearInterruptedGeneration();
        if (this.selectedPackageCode === 'free_preview') {
          this.previewCandidate = this.generatedCandidates[0] ?? null;
          this.rawPreviewAvailable = true;
          this.previewStyleName = this.selectedPortraitStyle?.style.name ?? null;
          this.previewSourceStoragePath = uploadResult.storagePath;
          this.trackVerticalFunnelEvent('vertical_pack_preview_generated', {
            candidateCount: this.generatedCandidates.length,
            style: this.previewStyleName,
          });
        } else {
          this.trackVerticalFunnelEvent('vertical_pack_paid_generation_confirmed', {
            candidateCount: this.generatedCandidates.length,
            style: this.selectedPortraitStyle?.style.name ?? null,
          });
        }
        if (this.areOutcomePackagesVisible) {
          this.loadPackageEntitlements();
        }
        this.isSaved = true;
        this.saveSuccessMessage = 'Headshot saved to your photo workspace successfully!';
        this._stateService.refreshGeneratedPhotosCount();
        this._cdr.detectChanges();

        finalResult = {
          status: 'succeeded',
          output: [resultCandidate!.imageUrl],
          dataUrl: resultCandidate!.imageUrl,
          processedImageId: resultCandidate!.processedImageId,
          storagePath: resultCandidate!.storagePath,
        };
      } else {
        const enhanceRequest = {
          // Keep URL for older backend behavior, but prefer storagePath so the API can
          // read the source image directly from storage in local/container runs.
          imageUrl: uploadResult.url,
          imageStoragePath: uploadResult.storagePath,
          enhancementType: this.enhancementType,
          turnstileToken: this.turnstileSiteKey ? this.turnstileToken : undefined,
        };

        const enhanceResponse = await firstValueFrom(
          this._replicateService.enhancePhoto(enhanceRequest)
        );

        if (!enhanceResponse?.success) {
          const errorMsg = enhanceResponse?.error?.message || 'Enhancement failed';
          console.error('Enhancement API failed:', errorMsg);
          throw new Error(errorMsg);
        }

        // Check if this is an OpenAI response (immediate result) or Replicate response (async)
        if (
          enhanceResponse.data?.provider === 'OpenAI' ||
          (enhanceResponse.data?.Status === 'succeeded' && !enhanceResponse.data?.prediction)
        ) {
          // OpenAI returns immediate results - no polling needed
          console.log('OpenAI immediate result detected');
          this.processingProgress = 75;
          this.processingStatus = 'Processing OpenAI result...';
          this._cdr.detectChanges();

          finalResult = {
            status: 'succeeded',
            output: enhanceResponse.data.Output,
            dataUrl: enhanceResponse.data.dataUrl,
            processedImageId: enhanceResponse.data.processedImageId,
            storagePath: enhanceResponse.data.storagePath,
          };
        } else {
          // Replicate async flow - requires polling
          if (!enhanceResponse?.data?.prediction?.id) {
            console.error('No prediction ID in response:', enhanceResponse);
            throw new Error('Enhancement failed - no prediction ID returned');
          }

          // Step 3: Poll for completion
          this.processingProgress = 50;
          this.processingStatus = 'AI is enhancing your photo...';
          this._cdr.detectChanges();

          const predictionId = enhanceResponse.data.prediction.id;
          finalResult = await this.pollForCompletion(predictionId);
        }
      }

      let enhancedUrl = null;

      // Handle output as string (new Replicate format) or array (legacy format)
      if (finalResult.output) {
        if (typeof finalResult.output === 'string') {
          enhancedUrl = finalResult.output;
        } else if (Array.isArray(finalResult.output) && finalResult.output.length > 0) {
          enhancedUrl = finalResult.output[0];
        }
      }

      // Fallback to dataUrl if no valid output
      if (!enhancedUrl && finalResult.dataUrl) {
        enhancedUrl = finalResult.dataUrl;
      }

      if (enhancedUrl) {
        const isBase64 = enhancedUrl.startsWith('data:image/');

        this.enhancedImage = this.createEnhancedImageViewModel(
          enhancedUrl,
          'enhanced',
          finalResult.processedImageId,
          finalResult.storagePath
        );

        // Update processing state
        this.isProcessing = false;
        this.processingProgress = 100;
        this.processingStatus = 'Enhancement complete!';

        if (isBase64) {
          // Multi-stage change detection for large base64 data
          this._cdr.detectChanges();
          setTimeout(() => {
            this._cdr.detectChanges();
          }, 50);
        } else {
          this._cdr.detectChanges();
        }

        if (this.isProfilePhotoScoreVisible && this.enhancedImage.processedImageId) {
          this.scoreGeneratedPhoto(this.enhancedImage.processedImageId);
        }
        if (this.isProfilePhotoScoreVisible) {
          this.scoreAllCandidates();
        }

        await this.refreshCreditState();
      } else {
        console.error('No enhanced image received from API response');
        throw new Error('No enhanced image received');
      }
    } catch (error: any) {
      this.handleEnhancementFailure(error);
    } finally {
      this._nextRequestIsRegeneration = false;
      if (this.turnstileSiteKey) {
        this.turnstileToken = '';
        this.turnstile?.reset();
        this._cdr.markForCheck();
      }
    }
  }

  private handleEnhancementFailure(error: any): void {
    console.error('Full enhancement error details:', {
      error,
      status: error.status,
      message: error.message,
      body: error.error,
      stack: error.stack,
      name: error.name,
    });

    this.isProcessing = false;
    if (error.status === 401) {
      if (error.error?.error?.code === 'EmailNotVerified') {
        this.isEmailConfirmed = false;
        this.errorMessage = '';
        this.verificationMessage =
          error.error.error.message || 'Verify your email address before generating photos.';
        this._cdr.detectChanges();
        return;
      }

      this._authService.logout();
      return;
    }

    this.errorMessage = this.getEnhancementErrorMessage(error);
    this._cdr.detectChanges();
  }

  private getEnhancementErrorMessage(error: any): string {
    if (error.message?.includes('Upload failed')) {
      return 'Failed to upload image. Please check your connection and try again.';
    }

    if (error.message?.includes('Enhancement failed')) {
      return 'AI enhancement failed. Please try again or contact support.';
    }

    if (error.message?.includes('Enhancement timed out')) {
      return 'Enhancement is taking longer than expected. Please try again.';
    }

    if (error.status === 401) {
      return 'Authentication failed. Please log in again.';
    }

    if (error.status === 403) {
      return 'Insufficient permissions or credits. Please check your account.';
    }

    const apiMessage = error.error?.error?.message ?? error.error?.message;
    if (apiMessage) {
      return apiMessage;
    }

    if (error.status === 400) {
      return 'We could not complete that request. Check the photo and package details, then try again.';
    }

    return error.message || 'Enhancement failed. Please try again.';
  }

  private async uploadImageForEnhancement(): Promise<{
    url: string;
    fileName: string;
    storagePath?: string;
  } | null> {
    if (!this.selectedFile) {
      return null;
    }

    return new Promise((resolve, reject) => {
      // Headshot preview sources must survive payment return/retry. Store them as durable uploads.
      // Legacy creative transforms keep temporary enhanced uploads to avoid gallery records.
      const useTemporaryEnhancedStorage = !(
        this.isHeadshotMvpEnabled && this.enhancementType === 'headshot'
      );
      this._fileUploadService
        .uploadSingleImage(this.selectedFile!, useTemporaryEnhancedStorage)
        .subscribe({
          next: result => {
            if (result.progress < 100) {
              this.processingProgress = Math.round(result.progress * 0.2);
              this._cdr.detectChanges();
            } else if (result.response) {
              if (result.response.success) {
                this.processingProgress = 20;
                this._cdr.detectChanges();
                resolve(result.response.data);
              } else {
                console.error('Upload failed - server returned success=false');
                reject(new Error('Upload failed - server returned success=false'));
              }
            }
          },
          error: error => {
            console.error('Upload error:', error.message || error);
            reject(error);
          },
        });
    });
  }

  private async pollForCompletion(predictionId: string): Promise<any> {
    const maxAttempts = 60; // 5 minutes max (5 second intervals)
    let attempts = 0;

    while (attempts < maxAttempts) {
      try {
        const statusResponse = await firstValueFrom(
          this._replicateService.getPredictionStatus(predictionId)
        );

        if (statusResponse?.success && statusResponse.data) {
          const prediction = statusResponse.data;

          // Update progress based on status
          if (prediction.status === 'processing') {
            this.processingProgress = Math.min(50 + attempts * 2, 90);
            this.processingStatus = 'AI is enhancing your photo...';
          } else if (prediction.status === 'succeeded') {
            this.processingProgress = 100;
            this.processingStatus = 'Enhancement complete!';

            // Support new backend: prefer dataUrl if present
            if (prediction.dataUrl) {
              return { ...prediction, output: [prediction.dataUrl] };
            }

            return prediction;
          } else if (prediction.status === 'failed') {
            console.error('Enhancement failed:', prediction.error);
            throw new Error(prediction.error || 'Enhancement failed');
          }
        }

        // Wait 5 seconds before next poll
        await new Promise(resolve => setTimeout(resolve, 5000));
        attempts++;
      } catch (error) {
        console.error('Polling error:', error);
        throw error;
      }
    }

    throw new Error('Enhancement timed out. Please try again.');
  }

  getSelectedCandidateCount(): number {
    return (
      this.packageOptions.find(option => option.code === this.selectedPackageCode)
        ?.includedCandidateCount ?? 1
    );
  }

  getCandidateRequestCountForSelectedPackage(): number {
    return Math.max(this.getRemainingCandidateSlots(), 1);
  }

  upgradeToPackage(packageCode: string): void {
    const option = this.packageOptions.find(item => item.code === packageCode);
    if (
      (this.previewCandidate && !this.rawPreviewAvailable) ||
      (this.resumablePreview && !this.resumablePreview.hasRawPreview)
    ) {
      this.errorMessage =
        'This preview expired. Start over to create a new photo before upgrading.';
      this._cdr.markForCheck();
      return;
    }

    const activePackageCode = this.getBestActivePaidPackageCode();
    if (
      activePackageCode &&
      !window.confirm(
        `You already have an unused ${this.getPackageLabel(activePackageCode)}. Buy another package anyway?`
      )
    ) {
      return;
    }

    if (!option?.internalCreditPackageId) {
      this.errorMessage = `${option?.name ?? 'This package'} is not available for checkout yet.`;
      this._cdr.markForCheck();
      return;
    }

    this.trackVerticalFunnelEvent('vertical_pack_checkout_start', { packageCode });
    this.persistPreviewDraft(packageCode);
    this._router.navigate(['/pricing'], {
      queryParams: {
        packageId: option.internalCreditPackageId,
        outcomePackage: option.code,
        previewId: this.previewCandidate?.processedImageId ?? null,
        returnUrl: `/app/enhance?useCase=${this.selectedUseCaseCode}&previewId=${this.previewCandidate?.processedImageId ?? ''}`,
      },
    });
  }

  private persistPreviewDraft(packageCode: string): void {
    if (!this.previewCandidate) {
      return;
    }

    sessionStorage.setItem(
      'enhanceUpgradeDraft',
      JSON.stringify({
        packageCode,
        styleName: this.previewStyleName,
        paidStyleName: this.selectedPortraitStyle?.style.name,
        sourceStoragePath: this.previewSourceStoragePath,
        useCaseCode: this.selectedUseCaseCode,
        candidate: this.previewCandidate,
      })
    );
  }

  selectCandidate(candidate: CandidateViewModel): void {
    this.selectedCandidateId = candidate.processedImageId;
    this.enhancedImage = this.createEnhancedImageViewModel(
      candidate.imageUrl,
      'enhanced',
      candidate.processedImageId,
      candidate.storagePath
    );
    if (candidate.previewUnavailable) {
      this.enhancedImage.loadFailed = true;
    }
    if (candidate.score) {
      this.generatedScore = candidate.score;
    } else if (this.isProfilePhotoScoreVisible) {
      this.scoreGeneratedPhoto(candidate.processedImageId);
    }
  }

  onBeforeImageError(): void {
    this.beforeImageLoadFailed = true;
    this._cdr.markForCheck();
  }

  onEnhancedImageError(): void {
    if (!this.enhancedImage) {
      return;
    }

    this.enhancedImage = this.imageViewModule.nextFailedImageState(this.enhancedImage);
    this._cdr.markForCheck();
  }

  private createEnhancedImageViewModel(
    url: string,
    type?: string,
    processedImageId?: number,
    storagePath?: string
  ): EnhancedImage {
    return this.imageViewModule.createImageView(url, type, processedImageId, storagePath);
  }

  normalizeDisplayImageUrl(url: string, storagePath?: string): string {
    return this.imageViewModule.normalizeDisplayImageUrl(url, storagePath);
  }

  private async loadAuthorizedCandidateImages(
    candidates: CandidateViewModel[]
  ): Promise<CandidateViewModel[]> {
    return Promise.all(
      candidates.map(async candidate => {
        if (!this.isPrivateCandidateImage(candidate)) {
          return candidate;
        }

        try {
          const blob = await firstValueFrom(
            this._headshotGenerationService.getOriginalCandidateImage(candidate.processedImageId)
          );
          const objectUrl = URL.createObjectURL(blob);
          this.candidateObjectUrls.add(objectUrl);
          return { ...candidate, imageUrl: objectUrl };
        } catch {
          return { ...candidate, previewUnavailable: true };
        }
      })
    );
  }

  private isPrivateCandidateImage(candidate: CandidateViewModel): boolean {
    return candidate.storagePath.split('/').includes('generated-private');
  }

  private revokeCandidateObjectUrls(): void {
    for (const url of this.candidateObjectUrls) {
      URL.revokeObjectURL(url);
    }
    this.candidateObjectUrls.clear();
  }

  private getStorageProxyUrl(storagePath?: string): string | null {
    if (!storagePath) {
      return null;
    }

    const normalizedPath = storagePath.replace(/^\/+/, '');
    return this.toApiImageUrl(`/profile-images/${normalizedPath}`);
  }

  private toApiImageUrl(path: string): string {
    const apiUrl = this._configService.getApiUrl();
    if (apiUrl.startsWith('http')) {
      const apiBase = apiUrl.replace(/\/api\/?$/i, '');
      return `${apiBase}${path}`;
    }

    return `${window.location.origin}${path}`;
  }

  private toCandidateViewModels(
    data: NonNullable<
      import('../../services/headshot-generation.service').HeadshotGenerationResponse['data']
    >
  ): CandidateViewModel[] {
    const candidates = data.candidates?.length
      ? data.candidates
      : [
          {
            imageUrl: data.imageUrl,
            storagePath: data.storagePath,
            processedImageId: data.processedImageId,
            provider: data.provider,
            model: data.model,
            correlationId: data.correlationId,
            useCaseCode: this.selectedUseCaseCode,
          },
        ];

    return candidates.map(candidate => ({ ...candidate }));
  }

  private mergeGeneratedCandidates(
    responseCandidates: CandidateViewModel[]
  ): CandidateViewModel | null {
    const promotedPreview = responseCandidates.find(candidate =>
      candidate.storagePath.split('/').includes('generated-private')
    );
    // Promotion replaces the watermarked free-preview record; it is not an extra slot.
    const merged =
      promotedPreview &&
      this.previewCandidate &&
      this.previewCandidate.processedImageId !== promotedPreview.processedImageId
        ? this.generatedCandidates.filter(
            candidate => candidate.processedImageId !== this.previewCandidate!.processedImageId
          )
        : [...this.generatedCandidates];
    if (promotedPreview) {
      this.previewCandidate = promotedPreview;
    }
    const existingIds = new Set(merged.map(candidate => candidate.processedImageId));
    for (const candidate of responseCandidates) {
      if (!existingIds.has(candidate.processedImageId)) {
        merged.push(candidate);
        existingIds.add(candidate.processedImageId);
      }
    }

    this.generatedCandidates = merged.slice(0, this.getSelectedCandidateCount());
    return (
      responseCandidates.find(
        candidate => !candidate.storagePath.split('/').includes('generated-private')
      ) ??
      promotedPreview ??
      this.generatedCandidates[0] ??
      null
    );
  }

  private replaceRegeneratedCandidate(
    responseCandidates: CandidateViewModel[],
    candidateBeingRegeneratedId: number | null
  ): CandidateViewModel | null {
    const existingIds = new Set(
      this.generatedCandidates.map(candidate => candidate.processedImageId)
    );
    const replacement =
      responseCandidates.find(candidate => !existingIds.has(candidate.processedImageId)) ??
      responseCandidates.at(-1) ??
      null;
    if (!replacement) {
      return null;
    }

    const targetIndex = this.generatedCandidates.findIndex(
      candidate => candidate.processedImageId === candidateBeingRegeneratedId
    );
    if (targetIndex < 0) {
      this.generatedCandidates = this.generatedCandidates.length
        ? [replacement, ...this.generatedCandidates.slice(1)]
        : [replacement];
    } else {
      this.generatedCandidates = this.generatedCandidates.map((candidate, index) =>
        index === targetIndex ? replacement : candidate
      );
    }
    return replacement;
  }

  private scoreAllCandidates(): void {
    for (const candidate of this.generatedCandidates) {
      this._profileWorkflowService.scoreProcessedImage(candidate.processedImageId).subscribe({
        next: response => {
          if (response.success) {
            candidate.score = response.data;
            this.applyBestShotRanking();
            this._cdr.markForCheck();
          }
        },
        error: error => console.warn('Candidate scoring failed', error),
      });
    }
  }

  private applyBestShotRanking(): void {
    this.generatedCandidates = [...this.generatedCandidates]
      .map(candidate => this.withRecommendation(candidate))
      .sort((a, b) => (b.recommendationScore ?? -1) - (a.recommendationScore ?? -1));
  }

  private withRecommendation(candidate: CandidateViewModel): CandidateViewModel {
    if (!candidate.score) {
      return candidate;
    }

    const roleWeights = this.getRoleWeights(this.selectedRole);
    const subscore = (code: string) =>
      candidate.score?.subscores.find(item => item.code === code)?.score ??
      candidate.score?.overallScore ??
      0;
    const roleFit = Math.round(
      candidate.score.overallScore * 0.55 +
        subscore('face_presence') * roleWeights.facePresence +
        subscore('lighting') * roleWeights.lighting +
        subscore('background') * roleWeights.background +
        subscore('platform_fit') * roleWeights.platformFit
    );

    return {
      ...candidate,
      recommendationScore: roleFit,
      recommendationReason: `${candidate.label || this.getSelectedUseCase().shortLabel + ' fit'}: ${this.getRoleRecommendationReason(this.selectedRole)} Platform exports prefer strong face presence, lighting, and crop readiness.`,
    };
  }

  private getRoleWeights(role: string): {
    facePresence: number;
    lighting: number;
    background: number;
    platformFit: number;
  } {
    switch (role) {
      case 'founder_executive':
      case 'finance':
      case 'legal_lawyer':
        return { facePresence: 0.16, lighting: 0.12, background: 0.1, platformFit: 0.07 };
      case 'creative_creator':
        return { facePresence: 0.12, lighting: 0.14, background: 0.06, platformFit: 0.13 };
      case 'healthcare_clinical':
        return { facePresence: 0.18, lighting: 0.12, background: 0.08, platformFit: 0.07 };
      default:
        return { facePresence: 0.14, lighting: 0.11, background: 0.08, platformFit: 0.12 };
    }
  }

  private getRoleRecommendationReason(role: string): string {
    switch (role) {
      case 'founder_executive':
        return 'prioritizes confident face presence and polished lighting.';
      case 'tech_engineering':
        return 'prioritizes clear, approachable framing for LinkedIn and internal profiles.';
      case 'healthcare_clinical':
        return 'prioritizes trust, face clarity, and clean lighting.';
      case 'realtor_sales':
        return 'prioritizes approachable framing and platform-ready crops.';
      case 'creative_creator':
        return 'prioritizes expressive lighting while keeping exports usable.';
      case 'legal_lawyer':
        return 'prioritizes conservative background, clarity, and professional polish.';
      case 'finance':
        return 'prioritizes credibility, clean background, and balanced lighting.';
      default:
        return 'balances professional clarity with platform-ready export fit.';
    }
  }

  getSelectedRoleLabel(): string {
    return (
      this.roleOptions.find(option => option.value === this.selectedRole)?.label ??
      'General professional'
    );
  }

  getSelectedRoleDescription(): string {
    return (
      this.roleOptions.find(option => option.value === this.selectedRole)?.description ??
      this.roleOptions[0].description
    );
  }

  isWorkflowOptionVisible(option: { visibleWhen: string }): boolean {
    return (
      (option.visibleWhen === 'headshot' && this.isHeadshotMvpEnabled) ||
      (option.visibleWhen === 'creative' && this.isCreativeStylePackVisible)
    );
  }

  getRecommendedCandidateReason(): string {
    return (
      this.generatedCandidates[0]?.recommendationReason ??
      `${this.getSelectedRoleLabel()} fit uses score, crop, lighting, face presence, and export readiness.`
    );
  }

  private scoreGeneratedPhoto(processedImageId: number): void {
    this.isScoringGeneratedPhoto = true;
    this._profileWorkflowService
      .scoreProcessedImage(processedImageId)
      .pipe(
        finalize(() => {
          this.isScoringGeneratedPhoto = false;
          this._cdr.markForCheck();
        })
      )
      .subscribe({
        next: response => {
          if (response.success) {
            this.generatedScore = response.data;
          }
        },
        error: error => console.warn('Generated profile photo scoring failed', error),
      });
  }

  getScoreDelta(): number | null {
    if (!this.profileScore || !this.generatedScore) {
      return null;
    }

    return this.generatedScore.overallScore - this.profileScore.overallScore;
  }

  getAdjustedImageStyle(): Record<string, string> {
    return {
      transform: `scale(${this.adjustmentZoom / 100}) rotate(${this.adjustmentRotate}deg)`,
      filter: `brightness(${this.adjustmentBrightness}%) contrast(${this.adjustmentContrast}%)`,
    };
  }

  resetAdjustments(): void {
    this.adjustmentZoom = 100;
    this.adjustmentRotate = 0;
    this.adjustmentBrightness = 100;
    this.adjustmentContrast = 100;
  }

  applyPremiumAugmentation(type: string): void {
    if (!this.arePremiumAugmentationsVisible) {
      this.errorMessage = 'Premium augmentation add-ons are not available in this rollout.';
      return;
    }

    if (!this.enhancedImage?.storagePath && !this.enhancedImage?.url) {
      this.errorMessage = 'Choose a generated candidate before applying a premium augmentation.';
      this._cdr.markForCheck();
      return;
    }

    const previousImage = this.enhancedImage;
    this.isApplyingPremiumAugmentation = true;
    this.processingStatus = 'Applying premium augmentation to selected candidate...';
    this.processingProgress = 35;
    this.errorMessage = '';
    const sourcePayload = this.enhancedImage.storagePath
      ? { imageStoragePath: this.enhancedImage.storagePath }
      : { imageUrl: this.enhancedImage.url };

    this._replicateService
      .enhancePhoto({
        ...sourcePayload,
        enhancementType: type,
        turnstileToken: this.turnstileSiteKey ? this.turnstileToken : undefined,
      })
      .subscribe({
        next: response => {
          if (!response?.success) {
            throw new Error(response?.error?.message || 'Premium augmentation failed');
          }
          const data = response.data;
          const currentImage = this.enhancedImage;
          const imageUrl = data?.dataUrl || data?.Output?.[0] || currentImage?.url || '';
          this.enhancedImage = this.createEnhancedImageViewModel(
            imageUrl,
            type,
            data?.processedImageId || currentImage?.processedImageId,
            data?.storagePath || currentImage?.storagePath
          );
          this.enhancedImage.previousDisplayUrl = previousImage?.displayUrl;
          this.enhancedImage.augmentationLabel = this.premiumAugmentations.find(
            item => item.type === type
          )?.label;
          const processedImageId = this.enhancedImage.processedImageId;
          if (this.isProfilePhotoScoreVisible && processedImageId) {
            this.scoreGeneratedPhoto(processedImageId);
          }
          this.processingProgress = 100;
          void this.refreshCreditState();
          if (this.areOutcomePackagesVisible) {
            this.loadPackageEntitlements();
          }
        },
        error: error => {
          const apiError = error?.error?.error;
          this.errorMessage =
            error?.error?.message ||
            apiError?.message ||
            error?.message ||
            'Premium augmentation failed.';
          if (apiError?.code === 'BotVerificationFailed') {
            this.turnstileToken = '';
            this.turnstile?.reset();
          }
          this.isApplyingPremiumAugmentation = false;
          this._cdr.markForCheck();
        },
        complete: () => {
          this.isApplyingPremiumAugmentation = false;
          this._cdr.markForCheck();
        },
      });
  }

  downloadPackage(): void {
    if (this.isDownloadingPackage) {
      return;
    }
    if (!this.enhancedImage?.processedImageId) {
      this.errorMessage = 'Choose a generated candidate before downloading the package.';
      this._cdr.markForCheck();
      return;
    }
    if (!this.canDownloadPackage()) {
      this.errorMessage =
        'This package export kit has already been used. Your photos remain in the workspace; contact support if the previous download did not complete.';
      this._cdr.markForCheck();
      return;
    }

    this.isDownloadingPackage = true;
    this._profileWorkflowService
      .createExportPackage(
        this.enhancedImage.processedImageId,
        Array.from(this.selectedExportCodes),
        {
          zoomPercent: this.adjustmentZoom,
          rotateDegrees: this.adjustmentRotate,
          brightnessPercent: this.adjustmentBrightness,
          contrastPercent: this.adjustmentContrast,
          sharpnessPercent: 100,
        }
      )
      .subscribe({
        next: blob => {
          const url = URL.createObjectURL(blob);
          const link = document.createElement('a');
          link.href = url;
          link.download = `profile-photo-package-${Date.now()}.zip`;
          link.click();
          URL.revokeObjectURL(url);
          this.exportSuccessMessage =
            'Package downloaded. Your selected crops are ready in your downloads.';
          if (this.areOutcomePackagesVisible) {
            this.loadPackageEntitlements();
          }
        },
        error: error => {
          console.warn('Package download failed', error);
          this.errorMessage =
            'The package download did not finish. Your export allowance was not changed; check your connection and try again.';
          this.isDownloadingPackage = false;
          this._cdr.markForCheck();
        },
        complete: () => {
          this.isDownloadingPackage = false;
          this._cdr.markForCheck();
        },
      });
  }

  toggleExport(code: string, checked: boolean): void {
    if (checked) {
      this.selectedExportCodes.add(code);
    } else {
      this.selectedExportCodes.delete(code);
    }
  }

  isExportSelected(code: string): boolean {
    return this.selectedExportCodes.has(code);
  }

  async downloadEnhanced() {
    if (!this.enhancedImage?.url) {
      this.errorMessage = 'No enhanced photo is ready to download yet.';
      this._cdr.markForCheck();
      return;
    }

    const fileName = `enhanced-photo-${Date.now()}.png`;

    try {
      if (this.enhancedImage.url.startsWith('data:image/')) {
        this.triggerDownload(this.enhancedImage.url, fileName);
        return;
      }

      const downloadUrl =
        this.enhancedImage.displayUrl ||
        this.getStorageProxyUrl(this.enhancedImage.storagePath) ||
        this.enhancedImage.url;
      this.triggerDownload(this.toAttachmentDownloadUrl(downloadUrl, fileName), fileName);
    } catch (error) {
      console.error('Enhanced photo download failed:', error);
      this.errorMessage = 'Enhanced photo is ready, but download failed. Please try again.';
      this._cdr.markForCheck();
    }
  }

  private toAttachmentDownloadUrl(url: string, fileName: string): string {
    try {
      const parsed = new URL(url, window.location.origin);
      parsed.searchParams.set('download', '1');
      parsed.searchParams.set('filename', fileName);
      return parsed.toString();
    } catch {
      return url;
    }
  }

  private triggerDownload(url: string, fileName: string): void {
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.rel = 'noopener';
    document.body.appendChild(link);
    link.click();
    link.remove();
  }

  async saveToGallery() {
    if (!this.enhancedImage || this.isSaving || this.isSaved) {
      return;
    }

    this.isSaving = true;
    this.saveSuccessMessage = '';
    this.errorMessage = '';
    this._cdr.markForCheck();

    try {
      const response = await this._fileUploadService.saveEnhancedImage(
        this.enhancedImage.url,
        this.enhancementType
      );

      if (response.success) {
        this.isSaved = true;
        this.saveSuccessMessage = 'Enhanced image saved to your photo workspace successfully!';
        // Refresh the workspace data in the photo workspace coordinator service
        this._stateService.forceRefresh();
      } else {
        this.errorMessage = response.error?.message || 'Failed to save enhanced image';
      }
    } catch (error: any) {
      console.error('Error saving enhanced image:', error);
      this.errorMessage = error?.message || 'Failed to save enhanced image to photo workspace';
    } finally {
      this.isSaving = false;
      this._cdr.markForCheck();
    }
  }
  shareEnhanced() {
    if (navigator.share && this.enhancedImage) {
      // If data URL, use Web Share API with files if supported
      if (this.enhancedImage.url.startsWith('data:image/')) {
        fetch(this.enhancedImage.url)
          .then(res => res.blob())
          .then(blob => {
            const file = new File([blob], 'enhanced-photo.png', { type: blob.type });
            navigator.share({
              title: 'My Enhanced Photo',
              text: 'Check out my AI-enhanced photo!',
              files: [file],
            });
          });
      } else {
        navigator.share({
          title: 'My Enhanced Photo',
          text: 'Check out my AI-enhanced photo!',
          url: this.enhancedImage.url,
        });
      }
    } else if (this.enhancedImage) {
      // Fallback: copy data URL to clipboard
      navigator.clipboard.writeText(this.enhancedImage.url);
      // Optionally show a toast notification
    }
  }

  enhanceAnother() {
    this.clearInterruptedGeneration();
    this.selectedFile = null;
    this.imagePreview = null;
    this.enhancedImage = null;
    this.selectedCandidateId = null;
    this.errorMessage = '';
    this.exportSuccessMessage = '';
    this.profileScore = null;
    this.generatedScore = null;
    this.generatedCandidates = [];
    this.resetAdjustments();
    this.isProcessing = false;
    this.processingProgress = 0;

    // Reset save state
    this.isSaving = false;
    this.isSaved = false;
    this.saveSuccessMessage = '';
  }

  resetComponent() {
    this.enhanceAnother();
    void this.refreshCreditState();
  }

  getNextResetText(resetDate: Date): string {
    const now = new Date();
    const reset = new Date(resetDate);
    const diffTime = reset.getTime() - now.getTime();
    const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));

    if (diffDays <= 0) {
      return 'very soon';
    } else if (diffDays === 1) {
      return 'tomorrow';
    } else {
      return `in ${diffDays} days`;
    }
  }

  private async refreshCreditState(): Promise<void> {
    try {
      await this._stateService.refreshCredits({ internalOnly: true });
    } catch (refreshError) {
      console.warn('Failed to refresh credits after enhancement', refreshError);
    }
  }

  private loadAccountStatus(): void {
    this.isLoadingAccountStatus = true;
    this._authService.getAccountStatus().subscribe({
      next: response => {
        if (response?.success && typeof response?.data?.emailConfirmed === 'boolean') {
          this.isEmailConfirmed = response.data.emailConfirmed;
        }
        this.isLoadingAccountStatus = false;
        this._cdr.markForCheck();
      },
      error: () => {
        // Non-blocking: backend will enforce verification for sensitive operations
        this.isLoadingAccountStatus = false;
        this._cdr.markForCheck();
      },
    });
  }

  resendVerificationEmail(): void {
    if (this.isResendingVerificationEmail) {
      return;
    }

    this.verificationMessage = '';
    this.isResendingVerificationEmail = true;
    this._cdr.markForCheck();

    this._authService
      .resendConfirmationEmail()
      .pipe(
        finalize(() => {
          this.isResendingVerificationEmail = false;
          this._cdr.markForCheck();
        })
      )
      .subscribe({
        next: response => {
          if (response?.success) {
            this.verificationMessage =
              'Verification email sent. Please check your inbox (and spam).';
          } else {
            this.verificationMessage =
              response?.error?.message || 'Failed to send verification email. Please try again.';
          }
          this._cdr.markForCheck();
        },
        error: error => {
          this.verificationMessage =
            error?.error?.error?.message ||
            error?.error?.message ||
            error?.message ||
            'Failed to send verification email. Please try again.';
          this._cdr.markForCheck();
        },
      });
  }
}

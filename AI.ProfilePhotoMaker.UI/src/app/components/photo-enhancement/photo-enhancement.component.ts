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
import { RouterModule } from '@angular/router';
import { ReplicateService } from '../../services/replicate.service';
import { FileUploadService } from '../../services/file-upload.service';
import {
  HeadshotCandidate,
  HeadshotGenerationService,
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
import {
  PlatformExportOption,
  OutcomePackageDefinition,
  PackageEntitlement,
  ProfilePhotoScore,
  ProfileWorkflowService,
} from '../../services/profile-workflow.service';

interface EnhancedImage {
  url: string;
  displayUrl: string;
  type?: string;
  processedImageId?: number;
  storagePath?: string;
  fallbackAttempted?: boolean;
  loadFailed?: boolean;
}

interface CandidateViewModel extends HeadshotCandidate {
  score?: ProfilePhotoScore;
  recommendationScore?: number;
  recommendationReason?: string;
}

interface PortraitStyleCard {
  style: Style;
  key: string;
  name: string;
  description: string;
  previewUrl: string;
  group: 'recommended' | 'more' | 'fun';
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
  profileScore: ProfilePhotoScore | null = null;
  generatedScore: ProfilePhotoScore | null = null;
  isScoringPhoto = false;
  isScoringGeneratedPhoto = false;
  selectedRole = 'general_professional';
  selectedPortraitStyle: PortraitStyleCard | null = null;
  selectedStyleGroup: 'recommended' | 'more' | 'fun' = 'recommended';
  portraitStyles: PortraitStyleCard[] = [];
  isLoadingPortraitStyles = false;
  portraitStyleError = '';
  selectedPackageCode: 'free_preview' | 'starter_package' | 'pro_package' = 'free_preview';
  generatedCandidates: CandidateViewModel[] = [];
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
  readonly photoAdjustments = [
    'Crop / reposition',
    'Zoom for profile avatars',
    'Rotate / straighten',
    'Brightness',
    'Contrast',
    'Sharpness',
  ];
  readonly premiumAugmentations = [
    { label: 'Relighting', type: 'relighting' },
    { label: 'Professional polish', type: 'professional_polish' },
    { label: 'Outfit upgrade', type: 'outfit_upgrade' },
    { label: 'Background upgrade', type: 'background_upgrade' },
  ];
  exportOptions: PlatformExportOption[] = [];
  selectedExportCodes = new Set<string>([
    'linkedin_profile',
    'google_avatar',
    'resume_headshot',
    'original_high_res',
  ]);
  isDownloadingPackage = false;
  adjustmentZoom = 100;
  adjustmentRotate = 0;
  adjustmentBrightness = 100;
  adjustmentContrast = 100;
  adjustmentSharpness = 100;
  cropOffsetX = 0;
  cropOffsetY = 0;

  // Save to workspace state
  isSaving = false;
  saveSuccessMessage = '';
  isSaved = false;

  private _stateSubscription!: Subscription;
  private _consentSubscription?: Subscription;
  private _selectedFileToken = 0;

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

  // Check if user has enough credits for selected enhancement
  hasEnoughCredits(): boolean {
    if (this.isHeadshotMvpEnabled && this.enhancementType === 'headshot') {
      return this.selectedPackageCode === 'free_preview' || this.hasSelectedPackageEntitlement();
    }

    const totalCredits = this.getTotalAvailableCredits();
    const requiredCredits = this.getRequiredCredits();
    return totalCredits >= requiredCredits;
  }

  hasSelectedPackageEntitlement(): boolean {
    if (this.selectedPackageCode === 'free_preview') {
      return true;
    }

    const requiredCandidates = this.getSelectedCandidateCount();
    return this.packageEntitlements.some(
      entitlement =>
        entitlement.packageCode === this.selectedPackageCode &&
        entitlement.status.toLowerCase() === 'active' &&
        entitlement.remainingPackageUses > 0 &&
        entitlement.remainingCandidates >= requiredCandidates
    );
  }

  hasPremiumAugmentationEntitlement(): boolean {
    return this.packageEntitlements.some(
      entitlement =>
        entitlement.status.toLowerCase() === 'active' &&
        entitlement.remainingPremiumAugmentations > 0
    );
  }

  canApplyPremiumAugmentation(): boolean {
    return (
      this.arePremiumAugmentationsVisible &&
      !this.isProcessing &&
      !!this.enhancedImage &&
      this.hasPremiumAugmentationEntitlement()
    );
  }

  getSelectedPackageLabel(): string {
    return (
      this.packageOptions.find(option => option.code === this.selectedPackageCode)?.name ??
      'Free Preview'
    );
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
      return 'Free Preview is available without internal credits and generates one preview candidate.';
    }

    const entitlement = this.packageEntitlements.find(
      item =>
        item.packageCode === this.selectedPackageCode && item.status.toLowerCase() === 'active'
    );
    if (!entitlement) {
      return `${this.getSelectedPackageLabel()} is locked until purchase grants an entitlement.`;
    }

    return `${entitlement.remainingCandidates} candidates, ${entitlement.remainingRefinements} refinements, ${entitlement.remainingPremiumAugmentations} premium augmentations, export kit ${entitlement.platformExportKitAvailable ? 'available' : 'used'}.`;
  }

  requiresTurnstile(): boolean {
    return !!this.turnstileSiteKey;
  }

  canStartEnhancement(): boolean {
    return (
      !this.isProcessing &&
      this.hasEnoughCredits() &&
      (!this.isHeadshotMvpEnabled || !!this.selectedPortraitStyle) &&
      (!this.requiresTurnstile() || !!this.turnstileToken) &&
      this.biometricConsentAccepted
    );
  }

  ngOnInit() {
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

  private loadPortraitStyles(): void {
    this.isLoadingPortraitStyles = true;
    this.portraitStyleError = '';
    this._styleService.getActiveStyles().subscribe({
      next: response => {
        if (response.success && response.data?.length) {
          this.portraitStyles = response.data
            .map(style => this.toPortraitStyleCard(style))
            .sort((a, b) => a.displayOrder - b.displayOrder || a.name.localeCompare(b.name));
          this.selectedPortraitStyle =
            this.portraitStyles.find(style => style.group === 'recommended') ??
            this.portraitStyles[0] ??
            null;
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
        group: 'recommended',
        badgeLabel: 'Healthcare',
        displayOrder: 60,
        description: 'Calm, trusted presentation for clinical profiles.',
      },
      academic: {
        group: 'recommended',
        badgeLabel: 'Academic',
        displayOrder: 70,
        description: 'Credible portrait for educators, authors, and researchers.',
      },
      creative: {
        group: 'recommended',
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
    return this.portraitStyles.filter(style => style.group === this.selectedStyleGroup);
  }

  hasPortraitStyleGroup(group: 'recommended' | 'more' | 'fun'): boolean {
    return this.portraitStyles.some(style => style.group === group);
  }

  selectPortraitStyle(style: PortraitStyleCard): void {
    this.selectedPortraitStyle = style;
    this.enhancementType = this.isHeadshotMvpEnabled ? 'headshot' : style.key;
    this._cdr.markForCheck();
  }

  setStyleGroup(group: 'recommended' | 'more' | 'fun'): void {
    if (!this.hasPortraitStyleGroup(group)) {
      return;
    }

    this.selectedStyleGroup = group;
    const visibleSelected = this.selectedPortraitStyle?.group === group;
    if (!visibleSelected) {
      this.selectedPortraitStyle = this.getVisiblePortraitStyles()[0] ?? this.selectedPortraitStyle;
    }
  }

  getStyleGroupLabel(group: 'recommended' | 'more' | 'fun'): string {
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
    this._profileWorkflowService.getEntitlements().subscribe({
      next: response => {
        if (response.success) {
          this.packageEntitlements = response.data;
        }
        this._cdr.markForCheck();
      },
      error: error => console.warn('Failed to load package entitlements', error),
    });
  }

  ngOnDestroy() {
    if (this._stateSubscription) {
      this._stateSubscription.unsubscribe();
    }
    this._consentSubscription?.unsubscribe();
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
    const fileToken = ++this._selectedFileToken;

    // Create preview
    const reader = new FileReader();
    reader.onload = e => {
      if (fileToken !== this._selectedFileToken || this.selectedFile !== file) {
        return;
      }

      this.imagePreview = e.target?.result as string;
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
    this._profileWorkflowService.scorePhoto(file).subscribe({
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
      complete: () => {
        if (fileToken === this._selectedFileToken && this.selectedFile === file) {
          this.isScoringPhoto = false;
          this._cdr.markForCheck();
        }
      },
    });
  }

  removeFile() {
    this._selectedFileToken++;
    this.selectedFile = null;
    this.imagePreview = null;
    this.errorMessage = '';
    this.profileScore = null;
    this.generatedScore = null;
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

    if (!this.selectedFile) {
      return;
    }

    if (!this.hasEnoughCredits()) {
      this.errorMessage = this.isHeadshotMvpEnabled
        ? `Unlock or select an available ${this.getSelectedPackageLabel()} entitlement before generating candidates.`
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
      // Step 1: Upload the image file
      this.processingStatus = 'Uploading image...';
      const uploadResult = await this.uploadImageForEnhancement();

      if (!uploadResult?.url) {
        throw new Error('Failed to upload image');
      }

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

        const headshotResponse = await firstValueFrom(
          this._headshotGenerationService.generateHeadshot({
            imageStoragePath: uploadResult.storagePath,
            style: this.selectedPortraitStyle?.style.name ?? 'linkedin',
            background: 'auto',
            packageCode: this.selectedPackageCode,
            numOutputs: this.getSelectedCandidateCount(),
            turnstileToken: this.turnstileSiteKey ? this.turnstileToken : undefined,
          })
        );

        if (!headshotResponse?.success || !headshotResponse.data?.imageUrl) {
          const errorMsg = headshotResponse?.error?.message || 'Headshot generation failed';
          console.error('Headshot API failed:', errorMsg);
          throw new Error(errorMsg);
        }

        this.processingProgress = 75;
        this.processingStatus = 'Preparing your headshot...';
        this.generatedCandidates = this.toCandidateViewModels(headshotResponse.data);
        if (this.areOutcomePackagesVisible) {
          this.loadPackageEntitlements();
        }
        this.isSaved = true;
        this.saveSuccessMessage = 'Headshot saved to your photo workspace successfully!';
        this._stateService.forceRefresh();
        this._cdr.detectChanges();

        finalResult = {
          status: 'succeeded',
          output: [headshotResponse.data.imageUrl],
          dataUrl: headshotResponse.data.imageUrl,
          processedImageId: headshotResponse.data.processedImageId,
          storagePath: headshotResponse.data.storagePath,
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

    this.errorMessage = this.getEnhancementErrorMessage(error);
    this.isProcessing = false;
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

    return error.error?.message || error.message || 'Enhancement failed. Please try again.';
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
      // Upload as enhanced image (isEnhanced=true) to prevent database records
      this._fileUploadService.uploadSingleImage(this.selectedFile!, true).subscribe({
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

  selectCandidate(candidate: CandidateViewModel): void {
    this.enhancedImage = this.createEnhancedImageViewModel(
      candidate.imageUrl,
      'enhanced',
      candidate.processedImageId,
      candidate.storagePath
    );
    if (candidate.score) {
      this.generatedScore = candidate.score;
    } else if (this.isProfilePhotoScoreVisible) {
      this.scoreGeneratedPhoto(candidate.processedImageId);
    }
  }

  onEnhancedImageError(): void {
    if (!this.enhancedImage || this.enhancedImage.loadFailed) {
      return;
    }

    const fallbackUrl = this.getStorageProxyUrl(this.enhancedImage.storagePath);
    if (
      !this.enhancedImage.fallbackAttempted &&
      fallbackUrl &&
      fallbackUrl !== this.enhancedImage.displayUrl
    ) {
      this.enhancedImage = {
        ...this.enhancedImage,
        displayUrl: fallbackUrl,
        fallbackAttempted: true,
      };
      this._cdr.markForCheck();
      return;
    }

    this.enhancedImage = {
      ...this.enhancedImage,
      loadFailed: true,
    };
    this._cdr.markForCheck();
  }

  private createEnhancedImageViewModel(
    url: string,
    type?: string,
    processedImageId?: number,
    storagePath?: string
  ): EnhancedImage {
    return {
      url,
      displayUrl: this.normalizeDisplayImageUrl(url, storagePath),
      type,
      processedImageId,
      storagePath,
    };
  }

  private normalizeDisplayImageUrl(url: string, storagePath?: string): string {
    if (!url) {
      return this.getStorageProxyUrl(storagePath) ?? '';
    }

    if (url.startsWith('data:image/')) {
      return url;
    }

    const storageProxyUrl = this.getStorageProxyUrl(storagePath);
    try {
      const parsed = new URL(url, window.location.origin);
      if (parsed.pathname.startsWith('/profile-images/')) {
        return this.toApiImageUrl(`${parsed.pathname}${parsed.search}`);
      }
    } catch {
      // Use storage proxy fallback below.
    }

    return url.startsWith('/') ? this.toApiImageUrl(url) : (storageProxyUrl ?? url);
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
          },
        ];

    return candidates.map(candidate => ({ ...candidate }));
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
      recommendationReason: `${this.getSelectedRoleLabel()} fit: ${this.getRoleRecommendationReason(this.selectedRole)} Platform exports prefer strong face presence, lighting, and crop readiness.`,
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
    this._profileWorkflowService.scoreProcessedImage(processedImageId).subscribe({
      next: response => {
        if (response.success) {
          this.generatedScore = response.data;
        }
      },
      error: error => console.warn('Generated profile photo scoring failed', error),
      complete: () => {
        this.isScoringGeneratedPhoto = false;
        this._cdr.markForCheck();
      },
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
      transform: `translate(${this.cropOffsetX}%, ${this.cropOffsetY}%) scale(${this.adjustmentZoom / 100}) rotate(${this.adjustmentRotate}deg)`,
      filter: `brightness(${this.adjustmentBrightness}%) contrast(${this.adjustmentContrast}%) saturate(${this.adjustmentSharpness}%)`,
    };
  }

  resetAdjustments(): void {
    this.adjustmentZoom = 100;
    this.adjustmentRotate = 0;
    this.adjustmentBrightness = 100;
    this.adjustmentContrast = 100;
    this.adjustmentSharpness = 100;
    this.cropOffsetX = 0;
    this.cropOffsetY = 0;
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

    this.isProcessing = true;
    this.processingStatus = 'Applying premium augmentation to selected candidate...';
    this.processingProgress = 35;
    this.errorMessage = '';
    this._replicateService
      .enhancePhoto({
        imageUrl: this.enhancedImage.url,
        imageStoragePath: this.enhancedImage.storagePath,
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
          this.errorMessage =
            error?.error?.message || error?.message || 'Premium augmentation failed.';
        },
        complete: () => {
          this.isProcessing = false;
          this._cdr.markForCheck();
        },
      });
  }

  downloadPackage(): void {
    if (!this.enhancedImage?.processedImageId || this.isDownloadingPackage) {
      this.downloadEnhanced();
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
          sharpnessPercent: this.adjustmentSharpness,
          cropOffsetXPercent: this.cropOffsetX,
          cropOffsetYPercent: this.cropOffsetY,
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
          if (this.areOutcomePackagesVisible) {
            this.loadPackageEntitlements();
          }
        },
        error: error => {
          console.warn('Package download failed; falling back to single image', error);
          this.downloadEnhanced();
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
    this.selectedFile = null;
    this.imagePreview = null;
    this.enhancedImage = null;
    this.errorMessage = '';
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

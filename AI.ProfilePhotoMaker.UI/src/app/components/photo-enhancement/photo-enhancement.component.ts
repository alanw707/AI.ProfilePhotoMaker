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
import { HeaderNavigationComponent } from '../../shared/header-navigation/header-navigation.component';
import { DashboardCoordinatorService } from '../../services/dashboard-coordinator.service';
import { CreditService, UserCreditStatus } from '../../services/credit.service';
import { ConfigService } from '../../services/config.service';
import { AuthService } from '../../services/auth.service';
import { TurnstileComponent } from '../../shared/turnstile/turnstile.component';
import { finalize, Subscription, firstValueFrom } from 'rxjs';
import { BiometricConsentService } from '../../services/biometric-consent.service';

interface EnhancedImage {
  url: string;
  type?: string;
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

  // Save to gallery state
  isSaving = false;
  saveSuccessMessage = '';
  isSaved = false;

  private _stateSubscription!: Subscription;
  private _consentSubscription?: Subscription;

  constructor(
    private _replicateService: ReplicateService,
    private _fileUploadService: FileUploadService,
    private _stateService: DashboardCoordinatorService,
    private _creditService: CreditService,
    private _configService: ConfigService,
    private _authService: AuthService,
    private _biometricConsentService: BiometricConsentService,
    private _cdr: ChangeDetectorRef
  ) {
    this.turnstileSiteKey = this._configService.turnstileSiteKey;
  }

  // Get total available credits from internal sources only
  getTotalAvailableCredits(): number {
    return this._creditService.getTotalAvailableCredits(
      this.userCreditStatus,
      null // No Replicate credits
    );
  }

  // Get required credits based on enhancement type
  getRequiredCredits(): number {
    return this._creditService.getCreditCostSync('photo_enhancement');
  }

  // Check if user has enough credits for selected enhancement
  hasEnoughCredits(): boolean {
    const totalCredits = this.getTotalAvailableCredits();
    const requiredCredits = this.getRequiredCredits();
    return totalCredits >= requiredCredits;
  }

  requiresTurnstile(): boolean {
    return !!this.turnstileSiteKey;
  }

  canStartEnhancement(): boolean {
    return (
      !this.isProcessing &&
      this.hasEnoughCredits() &&
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

    // Create preview
    const reader = new FileReader();
    reader.onload = e => {
      this.imagePreview = e.target?.result as string;
      this._cdr.detectChanges();
    };
    reader.onerror = e => {
      console.error('FileReader error:', e);
      this.errorMessage = 'Failed to read the image file.';
      this._cdr.detectChanges();
    };
    reader.readAsDataURL(file);
  }

  removeFile() {
    this.selectedFile = null;
    this.imagePreview = null;
    this.errorMessage = '';
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

    if (!this.selectedFile || !this.hasEnoughCredits()) {
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

      // Step 2: Call enhancement API
      this.processingProgress = 30;
      this.processingStatus = 'Starting AI enhancement...';

      const enhanceRequest = {
        // Pass through the URL; backend will convert to external HTTPS if needed
        imageUrl: uploadResult.url,
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

      let finalResult;

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

        this.enhancedImage = {
          url: enhancedUrl,
          type: 'enhanced',
        };

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

        await this.refreshCreditState();
      } else {
        console.error('No enhanced image received from API response');
        throw new Error('No enhanced image received');
      }
    } catch (error: any) {
      console.error('Full enhancement error details:', {
        error,
        status: error.status,
        message: error.message,
        body: error.error,
        stack: error.stack,
        name: error.name,
      });

      // Provide more specific error messages
      let errorMessage = 'Enhancement failed. Please try again.';

      if (error.message?.includes('Upload failed')) {
        errorMessage = 'Failed to upload image. Please check your connection and try again.';
      } else if (error.message?.includes('Enhancement failed')) {
        errorMessage = 'AI enhancement failed. Please try again or contact support.';
      } else if (error.message?.includes('Enhancement timed out')) {
        errorMessage = 'Enhancement is taking longer than expected. Please try again.';
      } else if (error.status === 401) {
        errorMessage = 'Authentication failed. Please log in again.';
      } else if (error.status === 403) {
        errorMessage = 'Insufficient permissions or credits. Please check your account.';
      } else if (error.error?.message) {
        errorMessage = error.error.message;
      } else if (error.message) {
        errorMessage = error.message;
      }

      this.errorMessage = errorMessage;
      this.isProcessing = false;
      this._cdr.detectChanges();
    } finally {
      if (this.turnstileSiteKey) {
        this.turnstileToken = '';
        this.turnstile?.reset();
        this._cdr.markForCheck();
      }
    }
  }

  private async uploadImageForEnhancement(): Promise<{ url: string; fileName: string } | null> {
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

  downloadEnhanced() {
    if (this.enhancedImage) {
      const link = document.createElement('a');
      link.href = this.enhancedImage.url;
      // If data URL, force PNG extension
      if (this.enhancedImage.url.startsWith('data:image/')) {
        link.download = `enhanced-photo-${Date.now()}.png`;
      } else {
        link.download = `enhanced-photo-${Date.now()}`;
      }
      link.click();
    }
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
        this.saveSuccessMessage = 'Enhanced image saved to gallery successfully!';
        // Refresh the gallery data in the dashboard coordinator service
        this._stateService.forceRefresh();
      } else {
        this.errorMessage = response.error?.message || 'Failed to save enhanced image';
      }
    } catch (error: any) {
      console.error('Error saving enhanced image:', error);
      this.errorMessage = error?.message || 'Failed to save enhanced image to gallery';
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

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
import { Router, RouterModule } from '@angular/router';
import { ReplicateService } from '../../services/replicate.service';
import { FileUploadService } from '../../services/file-upload.service';
import { AuthService } from '../../services/auth.service';
import { HeaderNavigationComponent } from '../../shared/header-navigation/header-navigation.component';
import { DashboardCoordinatorService } from '../../services/dashboard-coordinator.service';
import { CreditService, UserCreditStatus } from '../../services/credit.service';
import { Subscription } from 'rxjs';

interface EnhancedImage {
  url: string;
  type?: string;
}

@Component({
  selector: 'app-photo-enhancement',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, HeaderNavigationComponent],
  templateUrl: './photo-enhancement.component.html',
  styleUrls: ['./photo-enhancement.component.sass'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PhotoEnhancementComponent implements OnInit, OnDestroy {
  @ViewChild('fileInput') fileInput!: ElementRef<HTMLInputElement>;

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
  allowedTypes: string[] = ['image/jpeg', 'image/png', 'image/webp'];

  private _stateSubscription!: Subscription;

  constructor(
    private _replicateService: ReplicateService,
    private _fileUploadService: FileUploadService,
    private _authService: AuthService,
    private _router: Router,
    private _stateService: DashboardCoordinatorService,
    private _creditService: CreditService,
    private _cdr: ChangeDetectorRef
  ) {}

  // Get total available credits from internal sources only
  getTotalAvailableCredits(): number {
    return this._creditService.getTotalAvailableCredits(
      this.userCreditStatus,
      null // No Replicate credits
    );
  }

  // Check if user has enough credits for enhancement
  hasEnoughCredits(): boolean {
    const totalCredits = this.getTotalAvailableCredits();
    return totalCredits > 0;
  }

  ngOnInit() {
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
  }

  ngOnDestroy() {
    if (this._stateSubscription) {
      this._stateSubscription.unsubscribe();
    }
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

  async startEnhancement() {
    if (!this.selectedFile || !this.hasEnoughCredits()) {
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

      // Convert relative URL to absolute URL for Replicate API
      const fullImageUrl = uploadResult.url.startsWith('http')
        ? uploadResult.url
        : `https://awlocaldev.ngrok.app${uploadResult.url}`;

      const enhanceRequest = {
        imageUrl: fullImageUrl,
        enhancementType: this.enhancementType,
      };

      const enhanceResponse = await this._replicateService.enhancePhoto(enhanceRequest).toPromise();

      if (!enhanceResponse?.success) {
        const errorMsg = enhanceResponse?.error?.message || 'Enhancement failed';
        console.error('Enhancement API failed:', errorMsg);
        throw new Error(errorMsg);
      }

      if (!enhanceResponse?.data?.prediction?.id) {
        console.error('No prediction ID in response:', enhanceResponse);
        throw new Error('Enhancement failed - no prediction ID returned');
      }

      // Step 3: Poll for completion
      this.processingProgress = 50;
      this.processingStatus = 'AI is enhancing your photo...';
      this._cdr.detectChanges();

      const predictionId = enhanceResponse.data.prediction.id;

      const finalResult = await this.pollForCompletion(predictionId);

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

        // Clean up the temporary uploaded image since we now have the enhanced version
        this.cleanupTemporaryImage(uploadResult.fileName);
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
    }
  }

  private async uploadImageForEnhancement(): Promise<{ url: string; fileName: string } | null> {
    if (!this.selectedFile) {
      return null;
    }

    return new Promise((resolve, reject) => {
      // Upload as temporary file (isEnhanced=false) for enhancement processing
      this._fileUploadService.uploadSingleImage(this.selectedFile!, false).subscribe({
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
        const statusResponse = await this._replicateService
          .getPredictionStatus(predictionId)
          .toPromise();

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
  }

  /**
   * Verify UI state after enhancement completion
   */
  private verifyUIState(): void {
    // UI state verification removed - debug logging cleaned up
  }

  /**
   * Clean up temporary uploaded image after successful enhancement
   * This removes the temporary image file since we now have the enhanced version from Replicate
   */
  private cleanupTemporaryImage(fileName: string): void {
    if (!fileName) {
      return;
    }

    // Call backend API to delete the temporary file
    this._fileUploadService.deleteTemporaryEnhancedImage(fileName).subscribe({
      next: response => {
        if (response.success) {
        } else {
          console.warn('⚠️ Failed to cleanup temporary image:', response.message);
        }
      },
      error: error => {
        console.warn('⚠️ Error during temporary image cleanup:', error);
        // Don't throw error - cleanup failure shouldn't affect user experience
      },
    });
  }

  resetComponent() {
    this.enhanceAnother();
    this._stateService.loadCreditsOnly();
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
}

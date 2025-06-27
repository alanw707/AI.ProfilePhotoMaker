import { Component, OnInit, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { ReplicateService, CreditsInfo } from '../../services/replicate.service';
import { FileUploadService } from '../../services/file-upload.service';
import { AuthService } from '../../services/auth.service';
import { HeaderNavigationComponent } from '../../shared/header-navigation/header-navigation.component';
import { DashboardStateService } from '../../services/dashboard-state.service';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-photo-enhancement',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, HeaderNavigationComponent],
  templateUrl: './photo-enhancement.component.html',
  styleUrls: ['./photo-enhancement.component.sass']
})
export class PhotoEnhancementComponent implements OnInit {
  @ViewChild('fileInput') fileInput!: ElementRef<HTMLInputElement>;
  
  selectedFile: File | null = null;
  imagePreview: string | null = null;
  enhancementType: string = 'background';
  isProcessing: boolean = false;
  processingProgress: number = 0;
  processingStatus: string = '';
  enhancedImage: any = null;
  creditsInfo: CreditsInfo | null = null;
  errorMessage: string = '';
  isDragOver: boolean = false;

  private stateSubscription!: Subscription;

  constructor(
    private replicateService: ReplicateService,
    private fileUploadService: FileUploadService,
    private authService: AuthService,
    private router: Router,
    private stateService: DashboardStateService
  ) {}

  ngOnInit() {
    this.stateSubscription = this.stateService.state$.subscribe(state => {
      this.creditsInfo = state.creditsInfo;
    });
  }

  ngOnDestroy() {
    if (this.stateSubscription) {
      this.stateSubscription.unsubscribe();
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
    console.log('Processing file:', file.name, 'Type:', file.type, 'Size:', file.size);
    
    // Validate file
    if (!file.type.startsWith('image/')) {
      this.errorMessage = 'Please select a valid image file.';
      console.error('Invalid file type:', file.type);
      return;
    }

    if (file.size > 5 * 1024 * 1024) { // 5MB limit
      this.errorMessage = 'File size must be less than 5MB.';
      console.error('File too large:', file.size);
      return;
    }

    this.selectedFile = file;
    this.errorMessage = '';

    // Create preview
    const reader = new FileReader();
    reader.onload = (e) => {
      this.imagePreview = e.target?.result as string;
      console.log('Image preview created successfully');
    };
    reader.onerror = (e) => {
      console.error('FileReader error:', e);
      this.errorMessage = 'Failed to read the image file.';
    };
    reader.readAsDataURL(file);
  }

  removeFile() {
    this.selectedFile = null;
    this.imagePreview = null;
    this.errorMessage = '';
  }

  async startEnhancement() {
    if (!this.selectedFile || !this.creditsInfo || this.creditsInfo.availableCredits <= 0) {
      return;
    }

    this.isProcessing = true;
    this.processingProgress = 0;
    this.processingStatus = 'Uploading image...';
    this.errorMessage = '';

    try {
      console.log('Starting enhancement for:', this.selectedFile.name);
      
      // Step 1: Upload the image file
      this.processingStatus = 'Uploading image...';
      const uploadResult = await this.uploadImageForEnhancement();
      
      if (!uploadResult || !uploadResult.url) {
        throw new Error('Failed to upload image');
      }

      // Step 2: Call enhancement API
      this.processingProgress = 30;
      this.processingStatus = 'Starting AI enhancement...';
      
      // Convert relative URL to absolute URL for Replicate API
      const fullImageUrl = uploadResult.url.startsWith('http') 
        ? uploadResult.url 
        : `http://localhost:5035${uploadResult.url}`;
      
      const enhanceRequest = {
        imageUrl: fullImageUrl,
        enhancementType: this.enhancementType
      };

      const enhanceResponse = await this.replicateService.enhancePhoto(enhanceRequest).toPromise();
      
      if (!enhanceResponse?.success) {
        throw new Error(enhanceResponse?.error?.message || 'Enhancement failed');
      }

      // Step 3: Poll for completion
      this.processingProgress = 50;
      this.processingStatus = 'AI is enhancing your photo...';
      
      const predictionId = enhanceResponse.data.prediction.id;
      const finalResult = await this.pollForCompletion(predictionId);
      
      // Use dataUrl if present, otherwise fallback to output[0]
      let enhancedUrl = finalResult.output && finalResult.output.length > 0 ? finalResult.output[0] : null;
      if (finalResult.dataUrl) {
        enhancedUrl = finalResult.dataUrl;
      }
      if (enhancedUrl) {
        this.enhancedImage = {
          url: enhancedUrl,
          type: 'enhanced'
        };
        
        // Update credits info
        this.stateService.setState({
            creditsInfo: {
                ...this.creditsInfo,
                availableCredits: enhanceResponse.data.creditsRemaining
            }
        });
        this.isProcessing = false;
        this.processingProgress = 100;
        this.processingStatus = 'Enhancement complete!';
      } else {
        throw new Error('No enhanced image received');
      }

    } catch (error: any) {
      console.error('Full enhancement error details:', error);
      console.error('Error status:', error.status);
      console.error('Error message:', error.message);
      console.error('Error body:', error.error);
      
      this.errorMessage = error.error?.message || error.message || 'Enhancement failed. Please try again.';
      this.isProcessing = false;
    }
  }


  private async uploadImageForEnhancement(): Promise<{ url: string; fileName: string } | null> {
    if (!this.selectedFile) return null;

    return new Promise((resolve, reject) => {
      console.log('Starting file upload for:', this.selectedFile!.name);
      this.fileUploadService.uploadSingleImage(this.selectedFile!).subscribe({
        next: (result) => {
          console.log('Upload progress result:', result);
          if (result.progress < 100) {
            this.processingProgress = Math.round(result.progress * 0.2); // Upload is 20% of total progress
          } else if (result.response) {
            console.log('Upload response:', result.response);
            if (result.response.success) {
              console.log('Upload successful, URL:', result.response.data.url);
              resolve(result.response.data);
            } else {
              console.error('Upload failed - response not successful');
              reject(new Error('Upload failed'));
            }
          }
        },
        error: (error) => {
          console.error('Upload error:', error);
          reject(error);
        }
      });
    });
  }

  private async pollForCompletion(predictionId: string): Promise<any> {
    const maxAttempts = 60; // 5 minutes max (5 second intervals)
    let attempts = 0;

    while (attempts < maxAttempts) {
      try {
        const statusResponse = await this.replicateService.getPredictionStatus(predictionId).toPromise();
        if (statusResponse?.success && statusResponse.data) {
          const prediction = statusResponse.data;
          // Update progress based on status
          if (prediction.status === 'processing') {
            this.processingProgress = Math.min(50 + (attempts * 2), 90);
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
              files: [file]
            });
          });
      } else {
        navigator.share({
          title: 'My Enhanced Photo',
          text: 'Check out my AI-enhanced photo!',
          url: this.enhancedImage.url
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

  resetComponent() {
    this.enhanceAnother();
    this.stateService.loadInitialDashboardData();
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

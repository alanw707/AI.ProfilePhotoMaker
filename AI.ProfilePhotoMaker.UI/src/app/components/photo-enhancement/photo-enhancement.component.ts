import { Component, OnInit, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { ReplicateService, GenerateBasicImageRequest, CreditsInfo } from '../../services/replicate.service';
import { FileUploadService } from '../../services/file-upload.service';
import { AuthService } from '../../services/auth.service';
import { ThemeService } from '../../services/theme.service';

@Component({
  selector: 'app-photo-enhancement',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="photo-enhancement-container">
      <!-- Theme Toggle Button -->
      <button class="theme-toggle" (click)="toggleTheme()" [attr.aria-label]="'Switch to ' + (themeService.isDark() ? 'light' : 'dark') + ' theme'">
        <svg *ngIf="!themeService.isDark()" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M20.354 15.354A9 9 0 018.646 3.646 9.003 9.003 0 0012 21a9.003 9.003 0 008.354-5.646z"/>
        </svg>
        <svg *ngIf="themeService.isDark()" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 3v1m0 16v1m9-9h-1M4 12H3m15.364 6.364l-.707-.707M6.343 6.343l-.707-.707m12.728 0l-.707.707M6.343 17.657l-.707.707M16 12a4 4 0 11-8 0 4 4 0 018 0z"/>
        </svg>
      </button>

      <!-- Navigation Header -->
      <header class="dashboard-header">
        <div class="header-content">
          <div class="logo-section">
            <img src="Logo.PNG" alt="AI Profile Photo Maker" class="header-logo">
            <h1>AI Profile Photo Maker</h1>
          </div>
          <!-- Navigation Menu -->
          <nav class="nav-menu">
            <a routerLink="/dashboard" routerLinkActive="active" [routerLinkActiveOptions]="{exact: true}" class="nav-link">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
                <rect x="3" y="3" width="7" height="9" stroke="currentColor" stroke-width="2"/>
                <rect x="13" y="3" width="8" height="5" stroke="currentColor" stroke-width="2"/>
                <rect x="13" y="12" width="8" height="9" stroke="currentColor" stroke-width="2"/>
                <rect x="3" y="16" width="7" height="5" stroke="currentColor" stroke-width="2"/>
              </svg>
              Premium Studio
            </a>
            <a routerLink="/enhance" routerLinkActive="active" class="nav-link">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
                <circle cx="12" cy="12" r="3" stroke="currentColor" stroke-width="2"/>
                <path d="M12 1v6m0 6v6m11-7h-6m-6 0H1" stroke="currentColor" stroke-width="2"/>
              </svg>
              Enhance
            </a>
            <a routerLink="/gallery" routerLinkActive="active" class="nav-link">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
                <rect x="3" y="3" width="18" height="18" rx="2" ry="2" stroke="currentColor" stroke-width="2"/>
                <circle cx="8.5" cy="8.5" r="1.5" stroke="currentColor" stroke-width="2"/>
                <polyline points="21,15 16,10 5,21" stroke="currentColor" stroke-width="2"/>
              </svg>
              Gallery
            </a>
          </nav>

          <div class="user-section">
              <div class="user-info">
                <span class="user-name">{{userName}}</span>
                <span class="user-email">{{userEmail}}</span>
              </div>
              <button class="btn btn-logout" (click)="logout()">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
                  <path d="M16 17L21 12M21 12L16 7M21 12H9M9 21H5C3.89543 21 3 20.1046 3 19V5C3 3.89543 3.89543 3 5 3H9" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
                </svg>
                Logout
              </button>
            </div>
        </div>
      </header>

      <div class="photo-enhancement">
      <div class="enhancement-header">
        <h2>Basic Photo Enhancement</h2>
        <p>Upload your photo and enhance it with AI - background removal, lighting correction, and professional styling.</p>
        
        <!-- Basic Tier Credits Display -->
        <div class="credits-info" *ngIf="creditsInfo">
          <div class="credits-card">
            <div class="credits-icon">⚡</div>
            <div class="credits-content">
              <h3>{{creditsInfo.availableCredits}} Credits</h3>
              <p>Resets {{getNextResetText(creditsInfo.nextResetDate)}}</p>
            </div>
          </div>
        </div>
      </div>

      <!-- Upload Section -->
      <div class="upload-section" *ngIf="!selectedFile && !isProcessing">
        <div class="upload-area" 
             (click)="triggerFileUpload()"
             (dragover)="onDragOver($event)"
             (dragleave)="onDragLeave($event)"
             (drop)="onDrop($event)"
             [class.drag-over]="isDragOver">
          <div class="upload-icon">📸</div>
          <h3>Upload Your Photo</h3>
          <p>Drop your photo here or click to browse</p>
          <p class="upload-restrictions">JPG, PNG, WebP • Max 5MB • Best results with headshots</p>
          <input type="file" #fileInput accept="image/*" (change)="onFileSelected($event)" style="display: none">
        </div>
      </div>

      <!-- Preview and Enhancement Options -->
      <div class="enhancement-section" *ngIf="selectedFile && !isProcessing">
        <div class="preview-container">
          <div class="image-preview">
            <img [src]="imagePreview" alt="Preview" class="preview-image">
            <button class="remove-btn" (click)="removeFile()" title="Remove image">×</button>
          </div>
          
          <div class="enhancement-options">
            <h3>Enhancement Options</h3>
            <div class="options-grid">
              <label class="option-card" [class.selected]="enhancementType === 'background'">
                <input type="radio" name="enhancement" value="background" [(ngModel)]="enhancementType">
                <div class="option-content">
                  <div class="option-icon">🖼️</div>
                  <h4>Background Remover</h4>
                  <p>Remove/replace background with professional backdrop</p>
                </div>
              </label>
              
              <label class="option-card" [class.selected]="enhancementType === 'social'">
                <input type="radio" name="enhancement" value="social" [(ngModel)]="enhancementType">
                <div class="option-content">
                  <div class="option-icon">📱</div>
                  <h4>Social Media</h4>
                  <p>Perfect for Instagram, bright and engaging, iPhone 16 Pro quality</p>
                </div>
              </label>
              
              <label class="option-card" [class.selected]="enhancementType === 'cartoon'">
                <input type="radio" name="enhancement" value="cartoon" [(ngModel)]="enhancementType">
                <div class="option-content">
                  <div class="option-icon">🎨</div>
                  <h4>Cartoon Mode</h4>
                  <p>Fun animated style transformation</p>
                </div>
              </label>
            </div>

            <button class="btn btn-primary enhance-btn" 
                    (click)="startEnhancement()"
                    [disabled]="!creditsInfo || creditsInfo.availableCredits <= 0">
              <span *ngIf="creditsInfo && creditsInfo.availableCredits > 0">
                Enhance Photo (1 credit)
              </span>
              <span *ngIf="!creditsInfo || creditsInfo.availableCredits <= 0">
                No Credits Available
              </span>
            </button>
          </div>
        </div>
      </div>

      <!-- Processing State -->
      <div class="processing-section" *ngIf="isProcessing">
        <div class="processing-card">
          <div class="processing-spinner"></div>
          <h3>Enhancing Your Photo</h3>
          <p>AI is working its magic... This usually takes 30-60 seconds.</p>
          <div class="processing-progress">
            <div class="progress-bar" [style.width.%]="processingProgress"></div>
          </div>
          <p class="processing-status">{{processingStatus}}</p>
        </div>
      </div>

      <!-- Results Section -->
      <div class="results-section" *ngIf="enhancedImage">
        <h3>Enhancement Complete!</h3>
        <div class="before-after">
          <div class="comparison-item">
            <h4>Before</h4>
            <img [src]="imagePreview" alt="Original" class="comparison-image">
          </div>
          <div class="comparison-arrow">→</div>
          <div class="comparison-item">
            <h4>After</h4>
            <img [src]="enhancedImage.url" alt="Enhanced" class="comparison-image">
          </div>
        </div>
        
        <div class="result-actions">
          <button class="btn btn-primary" (click)="downloadEnhanced()">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
              <path d="M21 15V19C21 20.1046 20.1046 21 19 21H5C3.89543 21 3 20.1046 3 19V15" stroke="currentColor" stroke-width="2"/>
              <polyline points="7,10 12,15 17,10" stroke="currentColor" stroke-width="2"/>
              <line x1="12" y1="15" x2="12" y2="3" stroke="currentColor" stroke-width="2"/>
            </svg>
            Download Enhanced Photo
          </button>
          <button class="btn btn-secondary" (click)="shareEnhanced()">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
              <circle cx="18" cy="5" r="3" stroke="currentColor" stroke-width="2"/>
              <circle cx="6" cy="12" r="3" stroke="currentColor" stroke-width="2"/>
              <circle cx="18" cy="19" r="3" stroke="currentColor" stroke-width="2"/>
              <line x1="8.59" y1="13.51" x2="15.42" y2="17.49" stroke="currentColor" stroke-width="2"/>
              <line x1="15.41" y1="6.51" x2="8.59" y2="10.49" stroke="currentColor" stroke-width="2"/>
            </svg>
            Share
          </button>
          <button class="btn btn-outline" (click)="enhanceAnother()">
            Enhance Another Photo
          </button>
        </div>
      </div>

      <!-- No Credits State -->
      <div class="no-credits" *ngIf="creditsInfo && creditsInfo.availableCredits <= 0 && !selectedFile">
        <div class="no-credits-card">
          <div class="no-credits-icon">⏳</div>
          <h3>No Credits Available</h3>
          <p>Your basic enhancement credits will reset {{getNextResetText(creditsInfo.nextResetDate)}}.</p>
          <div class="upgrade-prompt">
            <p>Want unlimited enhancements?</p>
            <button class="btn btn-primary">Upgrade to Premium</button>
          </div>
        </div>
      </div>

      <!-- Error State -->
      <div class="error-section" *ngIf="errorMessage">
        <div class="error-card">
          <div class="error-icon">⚠️</div>
          <h3>Enhancement Failed</h3>
          <p>{{errorMessage}}</p>
          <button class="btn btn-outline" (click)="resetComponent()">Try Again</button>
        </div>
      </div>
      </div>
    </div>
  `,
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
  userName: string = '';
  userEmail: string = '';

  constructor(
    private replicateService: ReplicateService,
    private fileUploadService: FileUploadService,
    private authService: AuthService,
    private router: Router,
    public themeService: ThemeService
  ) {}

  ngOnInit() {
    this.loadCreditsInfo();
    this.loadUserInfo();
  }

  async loadCreditsInfo() {
    try {
      const response = await this.replicateService.getCredits().toPromise();
      if (response?.success) {
        this.creditsInfo = response.data;
      }
    } catch (error) {
      console.error('Failed to load credits info:', error);
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
        this.creditsInfo.availableCredits = enhanceResponse.data.creditsRemaining;
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
    this.loadCreditsInfo();
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

  loadUserInfo() {
    // Get user info from auth service
    this.authService.currentUser$.subscribe(user => {
      if (user) {
        this.userEmail = user.email;
        
        // Use firstName/lastName from JWT if available, otherwise use email prefix
        const jwtName = `${user.firstName || ''} ${user.lastName || ''}`.trim();
        
        if (jwtName) {
          this.userName = jwtName;
        } else {
          // Fallback: use part of email before @ symbol
          this.userName = user.email.split('@')[0] || 'User';
        }
      }
    });
  }

  toggleTheme() {
    this.themeService.toggleTheme();
  }

  logout() {
    this.authService.logout();
    this.router.navigate(['/login']);
  }

}
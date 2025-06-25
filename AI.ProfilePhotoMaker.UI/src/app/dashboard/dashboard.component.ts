import { Component, OnInit, OnDestroy, ViewChild, ElementRef } from '@angular/core';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../services/auth.service';
import { ThemeService } from '../services/theme.service';
import { GalleryImage } from '../components/photo-gallery/photo-gallery.component';
import { FileUploadService, ProcessedImage } from '../services/file-upload.service';
import { ProfileService, UserProfile } from '../services/profile.service';
import { ReplicateService, CreditsInfo } from '../services/replicate.service';
import { StyleService, Style } from '../services/style.service';
import { NotificationService } from '../services/notification.service';
import { ConfigService } from '../services/config.service';
import { CreditService, UserCreditStatus } from '../services/credit.service';
import JSZip from 'jszip';
import * as faceapi from 'face-api.js';

interface StyleOption {
  id: string;
  name: string;
  description: string;
  previewUrl: string;
  selected: boolean;
}

interface GeneratedPhoto {
  id: string;
  url: string;
  style: string;
  createdAt: Date;
}


@Component({
  selector: 'app-dashboard',
  imports: [CommonModule, RouterModule, FormsModule],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.sass'
})
export class DashboardComponent implements OnInit, OnDestroy {
  @ViewChild('fileInput') fileInput!: ElementRef<HTMLInputElement>;

  // User Info
  userName: string = '';
  userEmail: string = '';
  userProfile: UserProfile | null = null;
  creditsInfo: CreditsInfo | null = null;

  // Loading States
  isLoadingProfile: boolean = false;
  isLoadingCredits: boolean = false;
  isLoadingStyles: boolean = false;
  isLoadingImages: boolean = false;

  // Dashboard Stats
  uploadedImages: number = 0;
  selectedStyles: number = 0;
  generatedPhotos: GeneratedPhoto[] = [];
  modelStatus: string = 'Not Started';

  // Workflow State
  currentStep: number = 1;
  
  // File Upload
  selectedFiles: File[] = [];
  isUploading: boolean = false;
  uploadProgress: number = 0;
  isDragOver: boolean = false;

  // AI Training
  trainingProgress: number = 0;
  estimatedCompletion: string = '';
  trainingZipPath: string = '';
  isTrainingStarted: boolean = false;
  trainingId: string = '';

  // Style Selection
  imagesPerStyle: number = 2; // Default to 2 images per style
  availableStyles: StyleOption[] = [];

  // Generation
  isGenerating: boolean = false;
  isGeneratingBasic: boolean = false;
  isDownloadingZip: boolean = false;

  // Premium Package
  userCreditStatus: UserCreditStatus | null = null;


  // Photo Gallery
  galleryImages: GalleryImage[] = [];
  showGallery: boolean = false;
  
  // Uploaded Images Thumbnails
  uploadedImageThumbnails: Array<{id: number; url: string; fileName: string}> = [];

  constructor(
    private authService: AuthService,
    private router: Router,
    private route: ActivatedRoute,
    public themeService: ThemeService,
    private fileUploadService: FileUploadService,
    private profileService: ProfileService,
    private replicateService: ReplicateService,
    private styleService: StyleService,
    private notificationService: NotificationService,
    private configService: ConfigService,
    public creditService: CreditService
  ) {}

  initializeStyles() {
    this.availableStyles = [
      // Professional & Career Styles
      { id: '1', name: 'Corporate', description: 'Professional studio portrait in formal business attire with clean background', previewUrl: 'https://images.unsplash.com/photo-1556157382-97eda2d62296?w=150&h=150&fit=crop&crop=face', selected: false },
      { id: '2', name: 'Executive', description: 'High-end executive portrait with power pose and luxury office background', previewUrl: 'https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=150&h=150&fit=crop&crop=face', selected: false },
      { id: '3', name: 'Consultant', description: 'Friendly consultant portrait in smart-casual attire with approachable expression', previewUrl: 'https://images.unsplash.com/photo-1506794778202-cad84cf45f1d?w=150&h=150&fit=crop&crop=face', selected: false },
      { id: '4', name: 'LinkedIn', description: 'Professional LinkedIn-style headshot with confident and warm expression', previewUrl: 'https://images.unsplash.com/photo-1519085360753-af0119f7cbe7?w=150&h=150&fit=crop&crop=face', selected: false },
      { id: '5', name: 'Legal', description: 'Formal lawyer portrait in courtroom or law office setting', previewUrl: 'https://images.unsplash.com/photo-1573496359142-b8d87734a5a2?w=150&h=150&fit=crop&crop=face', selected: false },
      { id: '6', name: 'Medical', description: 'Healthcare professional portrait with lab coat and trustworthy expression', previewUrl: 'https://images.unsplash.com/photo-1500648767791-00dcc994a43e?w=150&h=150&fit=crop&crop=face', selected: false },
      { id: '7', name: 'Author', description: 'Intellectual author portrait with bookshelves or writing desk background', previewUrl: 'https://images.unsplash.com/photo-1560250097-0b93528c311a?w=150&h=150&fit=crop&crop=face', selected: false },
      
      // Modern Entrepreneur & Tech Styles
      { id: '8', name: 'Entrepreneur', description: 'Modern startup founder portrait in co-working space with confident energy', previewUrl: 'https://images.unsplash.com/photo-1492562080023-ab3db95bfbce?w=150&h=150&fit=crop&crop=face', selected: false },
      { id: '9', name: 'Startup', description: 'Casual-smart startup founder with t-shirt and blazer combination', previewUrl: 'https://images.unsplash.com/photo-1463453091185-61582044d556?w=150&h=150&fit=crop&crop=face', selected: false },
      { id: '10', name: 'Tech Professional', description: 'Tech professional portrait with laptop or code in background', previewUrl: 'https://images.unsplash.com/photo-1582750433449-648ed127bb54?w=150&h=150&fit=crop&crop=face', selected: false },
      { id: '11', name: 'Influencer', description: 'Trendy social media influencer portrait with engaging eye contact', previewUrl: 'https://images.unsplash.com/photo-1544005313-94ddf0286df2?w=150&h=150&fit=crop&crop=face', selected: false },
      { id: '12', name: 'Digital Nomad', description: 'Outdoor lifestyle portrait of remote worker with laptop in natural setting', previewUrl: 'https://images.unsplash.com/photo-1528892952291-009c663ce843?w=150&h=150&fit=crop&crop=face', selected: false },
      
      // Creative, Lifestyle & Expressive Styles
      { id: '13', name: 'Creative', description: 'Colorful and dynamic artist portrait with creative studio background', previewUrl: 'https://images.unsplash.com/photo-1521119989659-a83eee488004?w=150&h=150&fit=crop&crop=face', selected: false },
      { id: '14', name: 'Casual', description: 'Natural lifestyle photo in everyday clothing with warm lighting', previewUrl: 'https://images.unsplash.com/photo-1524504388940-b1c1722653e1?w=150&h=150&fit=crop&crop=face', selected: false },
      { id: '15', name: 'Artistic', description: 'Fine art portrait with dramatic lighting and stylized clothing', previewUrl: 'https://images.unsplash.com/photo-1488426862026-3ee34a7d66df?w=150&h=150&fit=crop&crop=face', selected: false },
      { id: '16', name: 'Edgy/Urban', description: 'Street-style portrait with gritty city background and edgy aesthetic', previewUrl: 'https://images.unsplash.com/photo-1531927557220-a9e23c1e4794?w=150&h=150&fit=crop&crop=face', selected: false },
      { id: '17', name: 'Glamour', description: 'Fashion-inspired glamorous portrait with studio lighting and luxury feel', previewUrl: 'https://images.unsplash.com/photo-1515886657613-9f3515b0c78f?w=150&h=150&fit=crop&crop=face', selected: false },
      
      // Lifestyle & Identity-Focused Styles
      { id: '18', name: 'Academic', description: 'Scholar portrait with books or chalkboard in academic setting', previewUrl: 'https://images.unsplash.com/photo-1517841905240-472988babdf9?w=150&h=150&fit=crop&crop=face', selected: false },
      { id: '19', name: 'Fitness', description: 'Athletic portrait in workout gear with energetic expression', previewUrl: 'https://images.unsplash.com/photo-1531891437562-4301cf35b7e4?w=150&h=150&fit=crop&crop=face', selected: false },
      { id: '20', name: 'Spiritual', description: 'Serene portrait in natural light with peaceful spiritual elements', previewUrl: 'https://images.unsplash.com/photo-1507591064344-4c6ce005b128?w=150&h=150&fit=crop&crop=face', selected: false }
    ];
  }

  async ngOnInit() {
    console.log('Dashboard ngOnInit');
    
    // Initialize styles with sanitized icons
    this.initializeStyles();
    
    // Check authentication first
    if (!this.authService.isAuthenticated()) {
      console.log('Not authenticated, redirecting to login');
      this.router.navigate(['/login']);
      return;
    }
    
    console.log('User is authenticated, loading dashboard data');
    
    // Load credit costs first (needed for calculations)
    await this.creditService.loadCreditCosts();
    
    this.loadUserInfo();
    this.loadDashboardData();
    this.loadCreditStatus();
    
    // Set up periodic training status checks every 30 seconds if training is in progress
    const trainingCheckInterval = setInterval(() => {
      if (this.modelStatus === 'training' || this.isTrainingStarted) {
        console.log('Periodic training status check...');
        this.checkTrainingStatus(); // Use regular check for periodic updates (faster)
      } else if (this.modelStatus === 'trained') {
        // Stop checking once training is complete
        clearInterval(trainingCheckInterval);
      }
    }, 30000); // Check every 30 seconds
  }

  loadUserInfo() {
    // Get user info from auth service
    this.authService.currentUser$.subscribe(user => {
      if (user) {
        this.userEmail = user.email;
        
        // Use firstName/lastName from JWT if available, otherwise wait for profile fallback
        const jwtName = `${user.firstName || ''} ${user.lastName || ''}`.trim();
        if (jwtName) {
          this.userName = jwtName;
        } else {
          // Immediate fallback to email username if no JWT names
          this.userName = this.userEmail.split('@')[0];
        }
      }
    });

    // Load user profile from API (also serves as fallback for user name)
    this.isLoadingProfile = true;
    this.profileService.getCurrentUserProfile().subscribe({
      next: (response) => {
        if (response.success) {
          this.userProfile = response.data;
          
          // Use profile name as fallback if JWT didn't provide firstName/lastName
          if (!this.userName && this.userProfile) {
            const profileName = `${this.userProfile.firstName || ''} ${this.userProfile.lastName || ''}`.trim();
            this.userName = profileName || this.userEmail.split('@')[0]; // Final fallback to email username
          }
        } else {
          this.notificationService.error('Profile Load Failed', 'Failed to load user profile information.');
        }
      },
      error: (error) => {
        console.error('Failed to load user profile:', error);
        
        // If both JWT and profile fail, fallback to email username
        if (!this.userName && this.userEmail) {
          this.userName = this.userEmail.split('@')[0];
        }
        
        this.notificationService.error('Profile Load Failed', 'Unable to connect to the server. Please check your connection and try again.');
      },
      complete: () => {
        this.isLoadingProfile = false;
      }
    });

    // Load credits information
    this.isLoadingCredits = true;
    this.replicateService.getCredits().subscribe({
      next: (response) => {
        if (response.success) {
          this.creditsInfo = response.data;
        } else {
          this.notificationService.error('Credits Load Failed', 'Failed to load credit information.');
        }
      },
      error: (error) => {
        console.error('Failed to load credits info:', error);
        this.notificationService.warning('Credits Unavailable', 'Unable to load credit information. Some features may be limited.');
      },
      complete: () => {
        this.isLoadingCredits = false;
      }
    });
  }

  loadDashboardData() {
    this.loadUserImages();
    this.checkModelStatusAndTraining();
    this.updateCurrentStep();
    // Note: Styles are loaded on-demand when user reaches step 3 (style selection)
  }

  updateCurrentStep() {
    // Step 1: Upload Images (required)
    if (this.uploadedImages === 0) {
      this.currentStep = 1;
      return;
    }

    // Step 2: Generate Images (includes style selection, training, generation, and results)
    if (this.uploadedImages > 0) {
      // Load styles on-demand when user reaches this step
      if (this.availableStyles.length === 0) {
        this.loadActiveStyles();
      }
      
      this.currentStep = 2;
      return;
    }

    // Default fallback
    this.currentStep = 1;
  }

  updateSelectedStyles() {
    this.selectedStyles = this.availableStyles.filter(s => s.selected).length;
  }

  // Credit calculation methods
  calculateTrainingCredits(): number {
    // If model is already trained, no training cost
    if (this.modelStatus === 'trained') return 0;
    return this.selectedStyles > 0 ? this.creditService.getCreditCostSync('model_training') : 0;
  }

  calculateGenerationCredits(): number {
    return this.selectedStyles * this.imagesPerStyle * this.creditService.getCreditCostSync('styled_generation');
  }

  calculateTotalCredits(): number {
    return this.calculateTrainingCredits() + this.calculateGenerationCredits();
  }

  getRemainingCredits(): number {
    if (!this.creditsInfo) return 0;
    return this.creditsInfo.availableCredits - this.calculateTotalCredits();
  }

  hasEnoughCredits(): boolean {
    return this.getRemainingCredits() >= 0;
  }

  // File Upload Methods
  triggerFileUpload() {
    this.fileInput.nativeElement.click();
  }

  onFileSelected(event: any) {
    const files = Array.from(event.target.files) as File[];
    this.addFiles(files);
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
    const files = Array.from(event.dataTransfer?.files || []) as File[];
    this.addFiles(files);
  }

  // Helper: Check image resolution and face presence
  async checkImageQuality(file: File): Promise<{valid: boolean, reason?: string}> {
    return new Promise((resolve) => {
      const img = new Image();
      img.onload = async () => {
        if (img.width < 1024 || img.height < 1024) {
          resolve({valid: false, reason: 'Image must be at least 1024x1024 pixels.'});
          return;
        }
        // Load face-api models if not already loaded
        if (!faceapi.nets.ssdMobilenetv1.params) {
          await faceapi.nets.ssdMobilenetv1.loadFromUri('/assets/models');
        }
        const detections = await faceapi.detectAllFaces(img);
        if (!detections || detections.length === 0) {
          resolve({valid: false, reason: 'No face detected. Please upload a clear selfie.'});
          return;
        }
        resolve({valid: true});
      };
      img.onerror = () => resolve({valid: false, reason: 'Could not load image.'});
      img.src = URL.createObjectURL(file);
    });
  }

  async addFiles(files: File[]) {
    const validFiles: File[] = [];
    for (const file of files) {
      if (!file.type.startsWith('image/') || file.size > 5 * 1024 * 1024) continue;
      const quality = await this.checkImageQuality(file);
      if (!quality.valid) {
        this.notificationService.warning('Image Rejected', quality.reason || 'Image did not meet requirements.');
        continue;
      }
      validFiles.push(file);
    }
    // Limit to 20 total files
    const remainingSlots = 20 - this.selectedFiles.length;
    const filesToAdd = validFiles.slice(0, remainingSlots);
    this.selectedFiles.push(...filesToAdd);
  }

  removeFile(index: number) {
    this.selectedFiles.splice(index, 1);
  }

  getFilePreview(file: File): string {
    return URL.createObjectURL(file);
  }

  async uploadImages() {
    if (this.selectedFiles.length === 0) {
      this.notificationService.warning('No Files Selected', 'Please select at least one image to upload.');
      return;
    }

    if (this.selectedFiles.length < 5) {
      this.notificationService.warning('More Images Recommended', 'For best results, we recommend uploading at least 5-10 high-quality selfies.');
    }

    this.isUploading = true;
    this.uploadProgress = 0;
    
    try {
      // Get profile data for upload
      const profileData = this.userProfile ? {
        firstName: this.userProfile.firstName,
        lastName: this.userProfile.lastName,
        gender: this.userProfile.gender,
        ethnicity: this.userProfile.ethnicity
      } : undefined;

      this.fileUploadService.uploadImages(this.selectedFiles, profileData, true).subscribe({
        next: (result) => {
          this.uploadProgress = result.progress;
          
          if (result.response) {
            // Upload completed successfully
            const uploadedCount = result.response.uploadedFiles.length;
            this.selectedFiles = [];
            
            // Update user profile if we got profile ID back
            if (result.response.profileId && this.userProfile) {
              this.userProfile.id = result.response.profileId;
            }
            
            // Check if training ZIP was created
            if (result.response.zipCreated) {
              this.notificationService.success('Upload Complete', 
                `${uploadedCount} images uploaded successfully. Ready to select styles and start training!`);
              // Store ZIP path for later use when starting training
              this.trainingZipPath = result.response.zipPath;
            } else {
              this.notificationService.success('Upload Complete', 
                `${uploadedCount} images uploaded successfully.`);
            }
            
            // Refresh all user images and stats from server
            this.loadUserImages();
            this.updateCurrentStep();
            this.loadTrainingStatus(); // Check if we can start training
            this.isUploading = false;
          }
        },
        error: (error) => {
          console.error('Upload failed:', error);
          const errorMessage = error.error?.message || error.message || 'Upload failed. Please try again.';
          this.notificationService.error('Upload Failed', errorMessage);
          this.isUploading = false;
          this.uploadProgress = 0;
        }
      });
    } catch (error: any) {
      console.error('Upload failed:', error);
      this.notificationService.uploadError('An unexpected error occurred during upload.');
      this.isUploading = false;
    }
  }

  async startModelTraining(zipPath: string) {
    console.log('startModelTraining called with zipPath:', zipPath);
    
    if (!zipPath) {
      console.error('No training ZIP path provided');
      return;
    }

    // Convert local path to public URL
    const userId = this.getCurrentUserId();
    console.log('User ID for training:', userId);
    
    if (!userId) {
      console.error('No user ID available for training');
      this.notificationService.error('Authentication Error', 'User not authenticated. Please log in again.');
      return;
    }

    // Extract filename from the zipPath and construct public URL accessible by Replicate
    const fileName = zipPath.split(/[/\\]/).pop();
    const zipUrl = `${this.configService.externalBaseUrl}/training-zips/${fileName}`;
    console.log('Training ZIP URL (external):', zipUrl);

    const trainingRequest = {
      userId: userId,
      imageZipUrl: zipUrl
    };

    console.log('Starting model training with request:', trainingRequest);
    this.modelStatus = 'training';
    this.trainingProgress = 5; // Show initial progress
    this.isTrainingStarted = true;

    this.replicateService.trainModel(trainingRequest).subscribe({
      next: (response) => {
        console.log('Training API response:', response);
        if (response.success) {
          this.notificationService.success('Training Started', 
            `AI model training has begun with ${this.selectedStyles} selected styles. This will take 15-25 minutes.`);
          this.modelStatus = 'training';
          // Training progress is now shown within step 2
          // Store training ID for status polling
          if (response.data?.id) {
            this.trainingId = response.data.id;
            this.startTrainingStatusPolling();
          }
          this.updateCurrentStep();
        } else {
          console.error('Training failed:', response.error);
          this.notificationService.error('Training Failed', 
            response.error?.message || 'Failed to start model training.');
          this.modelStatus = 'ready';
          this.isTrainingStarted = false;
        }
      },
      error: (error) => {
        console.error('Training request failed:', error);
        this.notificationService.error('Training Failed', 
          'Failed to start model training. Please try again.');
        this.modelStatus = 'ready';
        this.isTrainingStarted = false;
      }
    });
  }

  private getCurrentUserId(): string | null {
    // Get current user ID from auth service
    const token = this.authService.getToken();
    if (!token) return null;
    
    try {
      // Decode JWT token to get user ID
      const payload = JSON.parse(atob(token.split('.')[1]));
      return payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'] || 
             payload['sub'] || 
             payload['user_id'] || 
             null;
    } catch (error) {
      console.error('Failed to decode token:', error);
      return null;
    }
  }

  private trainingStatusInterval: any;

  private startTrainingStatusPolling() {
    if (!this.trainingId) return;

    // Clear any existing polling
    if (this.trainingStatusInterval) {
      clearInterval(this.trainingStatusInterval);
    }

    // Poll every 30 seconds
    this.trainingStatusInterval = setInterval(() => {
      this.checkTrainingProgress();
    }, 30000);

    // Also check immediately
    this.checkTrainingProgress();
  }

  private checkTrainingProgress() {
    if (!this.trainingId) return;

    this.replicateService.getTrainingStatus(this.trainingId).subscribe({
      next: (response) => {
        if (response.success && response.data) {
          const status = response.data.status;
          console.log('Training status update:', status);

          if (status === 'succeeded' || status === 'completed') {
            // Training completed successfully
            this.modelStatus = 'trained';
            this.trainingProgress = 100;
            
            // Update user profile with trained model
            if (response.data.version && this.userProfile) {
              this.userProfile.trainedModelId = response.data.version;
            }

            // Stop polling
            if (this.trainingStatusInterval) {
              clearInterval(this.trainingStatusInterval);
              this.trainingStatusInterval = null;
            }

            // Notify user that training is complete
            this.notificationService.success('Training Complete!', 
              'Your AI model is now ready. You can now generate photos with your selected styles.');
            
            this.updateCurrentStep();
            
            // Model is now trained, stay in step 2 for generation
            // User can now select styles and generate

          } else if (status === 'failed' || status === 'canceled') {
            // Training failed
            this.modelStatus = 'failed';
            this.isTrainingStarted = false;
            
            // Stop polling
            if (this.trainingStatusInterval) {
              clearInterval(this.trainingStatusInterval);
              this.trainingStatusInterval = null;
            }

            this.notificationService.error('Training Failed', 
              'AI model training failed. Please try again with different photos.');
            
          } else if (status === 'processing' || status === 'starting') {
            // Training is still in progress
            this.modelStatus = 'training';
            // Update progress based on status (rough estimates)
            if (status === 'starting') {
              this.trainingProgress = Math.max(this.trainingProgress, 10);
            } else if (status === 'processing') {
              this.trainingProgress = Math.min(Math.max(this.trainingProgress, 30), 90);
            }
          }
        }
      },
      error: (error) => {
        console.error('Failed to check training progress:', error);
        // Don't stop polling on error - might be temporary network issue
      }
    });
  }

  async loadTrainingStatus() {
    try {
      this.fileUploadService.getTrainingStatus().subscribe({
        next: (status) => {
          console.log('Training status:', status);
          this.modelStatus = status.status;
          
          if (status.hasTrainedModel) {
            this.userProfile = this.userProfile || {} as UserProfile;
            this.userProfile.trainedModelId = status.trainedModelId;
            this.uploadedImages = status.totalUploadedImages;
          }
          
          this.updateCurrentStep();
        },
        error: (error) => {
          console.error('Failed to load training status:', error);
        }
      });
    } catch (error) {
      console.error('Error loading training status:', error);
    }
  }

  // Style Selection Methods
  async toggleStyle(style: StyleOption) {
    // If selecting a new style, check if user has sufficient credits
    if (!style.selected) {
      // Calculate what the total cost would be with this additional style
      const currentSelectedCount = this.availableStyles.filter(s => s.selected).length;
      const newSelectedCount = currentSelectedCount + 1;
      const trainingCost = this.modelStatus === 'trained' ? 0 : this.creditService.getCreditCostSync('model_training');
      const generationCost = newSelectedCount * this.imagesPerStyle * this.creditService.getCreditCostSync('styled_generation');
      const totalCostWithNewStyle = trainingCost + generationCost;
      
      // Check available credits
      const availableCredits = this.creditsInfo?.availableCredits || 0;
      
      if (totalCostWithNewStyle > availableCredits) {
        this.notificationService.error(
          'Insufficient Credits', 
          `Selecting this style would require ${totalCostWithNewStyle} credits, but you only have ${availableCredits} credits available. Please purchase more credits to continue.`
        );
        return;
      }
    }
    
    style.selected = !style.selected;
    this.updateSelectedStyles();
    // Note: We don't save immediately - only save when training starts
  }

  saveStyleSelection() {
    const selectedStyleIds = this.availableStyles
      .filter(style => style.selected)
      .map(style => parseInt(style.id));

    this.styleService.selectStyles({ styleIds: selectedStyleIds }).subscribe({
      next: (response) => {
        if (response.success) {
          console.log('Style selection saved successfully');
        }
      },
      error: (error) => {
        console.error('Failed to save style selection:', error);
      }
    });
  }

  selectAllStyles() {
    // Check if selecting all styles would exceed available credits
    const trainingCost = this.modelStatus === 'trained' ? 0 : this.creditService.getCreditCostSync('model_training');
    const generationCost = this.availableStyles.length * this.imagesPerStyle * this.creditService.getCreditCostSync('styled_generation');
    const totalCost = trainingCost + generationCost;
    const availableCredits = this.creditsInfo?.availableCredits || 0;

    if (totalCost > availableCredits) {
      this.notificationService.error(
        'Insufficient Credits', 
        `Selecting all styles would require ${totalCost} credits, but you only have ${availableCredits} credits available. Please purchase more credits or select fewer styles.`
      );
      return;
    }

    this.availableStyles.forEach(style => style.selected = true);
    this.updateSelectedStyles();
    // Note: We don't save immediately - only save when training starts
  }

  deselectAllStyles() {
    this.availableStyles.forEach(style => style.selected = false);
    this.updateSelectedStyles();
    // Note: We don't save immediately - only save when training starts
  }

  async startTrainingWithStyles() {
    console.log('Start Training button clicked!');
    console.log('Training conditions:', {
      trainingZipPath: this.trainingZipPath,
      selectedStyles: this.selectedStyles,
      uploadedImages: this.uploadedImages,
      modelStatus: this.modelStatus,
      isTrainingStarted: this.isTrainingStarted,
      hasEnoughCredits: this.hasEnoughCredits()
    });

    if (this.selectedStyles === 0) {
      console.log('No styles selected');
      this.notificationService.warning('No Styles Selected', 'Please select at least one style before starting training.');
      return;
    }

    // Check if model is already trained - prevent re-training
    if (this.modelStatus === 'trained') {
      console.log('Model already trained, redirecting to generation');
      this.notificationService.info('Model Already Trained', 'Your model is already trained! You can now generate photos with your selected styles.');
      // Auto-start generation with selected styles
      this.generatePhotos();
      return;
    }

    // Check if we have uploaded images but no training ZIP
    if (!this.trainingZipPath && this.uploadedImages >= 4) {
      console.log('No training ZIP found, creating one from uploaded images...');
      this.notificationService.info('Preparing Training Package', 'Creating training package from your uploaded images...');
      
      try {
        const zipResponse = await this.fileUploadService.createTrainingZip().toPromise();
        if (zipResponse?.success && zipResponse.zipPath) {
          this.trainingZipPath = zipResponse.zipPath;
          console.log('Training ZIP created successfully:', this.trainingZipPath);
          this.notificationService.success('Training Package Ready', zipResponse.message);
        } else {
          console.error('Failed to create training ZIP:', zipResponse);
          this.notificationService.error('Training Package Failed', 'Failed to create training package from uploaded images.');
          return;
        }
      } catch (error) {
        console.error('Error creating training ZIP:', error);
        this.notificationService.error('Training Package Error', 'An error occurred while creating the training package.');
        return;
      }
    }

    if (!this.trainingZipPath) {
      console.log('No training ZIP path available');
      this.notificationService.warning('Training Package Not Ready', 'Please upload at least 4 images first to create a training package.');
      return;
    }

    console.log('Starting training process...');
    
    // Save style selection before starting training
    this.saveStyleSelection();
    
    // Start training with selected styles
    this.startModelTraining(this.trainingZipPath);
  }

  async generatePhotos() {
    console.log('generatePhotos() called');
    console.log('userProfile:', this.userProfile);
    console.log('trainedModelVersionId:', this.userProfile?.trainedModelVersionId);
    console.log('trainedModelId:', this.userProfile?.trainedModelId);
    console.log('selectedStyles:', this.selectedStyles);
    
    // Check what the API is actually returning for this user
    this.profileService.getCurrentUserProfile().subscribe({
      next: (response) => {
        console.log('Fresh profile from API:', response);
        if (response.success && response.data) {
          console.log('API trainedModelId:', response.data.trainedModelId);
          console.log('API trainedModelVersionId:', response.data.trainedModelVersionId);
        }
      },
      error: (error) => {
        console.error('Error fetching fresh profile:', error);
      }
    });
    
    if (!this.userProfile?.trainedModelVersionId) {
      console.log('No trainedModelVersionId found, checking trainedModelId fallback');
      if (!this.userProfile?.trainedModelId) {
        this.notificationService.warning('Model Not Ready', 'Please wait for model training to complete before generating photos.');
        return;
      }
      // Fallback to trainedModelId if trainedModelVersionId is not available
      console.log('Using trainedModelId as fallback:', this.userProfile.trainedModelId);
    }

    if (this.selectedStyles === 0) {
      this.notificationService.warning('No Styles Selected', 'Please select at least one style before generating photos.');
      return;
    }

    const userId = this.getCurrentUserId();
    if (!userId) {
      this.notificationService.error('Authentication Error', 'User ID not found. Please log in again.');
      return;
    }

    // Get the model version to use for generation
    let modelVersion = this.userProfile.trainedModelVersionId || this.userProfile.trainedModelId;
    
    // TEMPORARY FIX: If we got the model name instead of version ID, use the known version ID
    if (modelVersion === 'alanw707/user-b99678bd-cb87-40c1-a7bf-b889f1e00c08-20250624130213') {
      modelVersion = '787e9b51e9a943dca35ea5be25d62c10db35af6d43e0b15336a36682c75bc024';
      console.log('Applied temporary fix: using known version ID');
    }
    
    if (!modelVersion) {
      this.notificationService.error('Model Version Error', 'No trained model version found. Please try refreshing the page.');
      return;
    }

    console.log('Using model version for generation:', modelVersion);

    this.isGenerating = true;
    this.updateCurrentStep(); // Move to generation step
    
    const selectedStyleNames = this.availableStyles
      .filter(style => style.selected)
      .map(style => style.name);

    try {
      let successCount = 0;
      let errorCount = 0;
      const totalImages = selectedStyleNames.length * this.imagesPerStyle;
      
      // Generate photos for each selected style
      for (const styleName of selectedStyleNames) {
        // Generate the specified number of images per style
        for (let i = 0; i < this.imagesPerStyle; i++) {
          const generateRequest = {
            trainedModelVersion: modelVersion,
            userId: userId,
            style: styleName,
            userInfo: {
              gender: this.userProfile.gender,
              ethnicity: this.userProfile.ethnicity
            }
          };

          this.replicateService.generateImages(generateRequest).subscribe({
            next: (response) => {
              if (response.success) {
                successCount++;
                console.log(`Successfully started generation for ${styleName} style #${i + 1}`);
                
                // Check if all generations have been initiated
                if (successCount + errorCount === totalImages) {
                  this.checkGenerationCompletion(successCount, errorCount, totalImages);
                }
              } else {
                errorCount++;
                this.notificationService.error('Generation Failed', `Failed to start ${styleName} style #${i + 1} generation.`);
                
                if (successCount + errorCount === totalImages) {
                  this.checkGenerationCompletion(successCount, errorCount, totalImages);
                }
              }
            },
            error: (error) => {
              console.error(`Failed to generate ${styleName} style #${i + 1}:`, error);
              errorCount++;
              const errorMessage = error.error?.message || error.message || 'Unknown error';
              this.notificationService.error('Generation Error', `${styleName} style #${i + 1}: ${errorMessage}`);
              
              if (successCount + errorCount === totalImages) {
                this.checkGenerationCompletion(successCount, errorCount, totalImages);
              }
            }
          });
        }
      }
      
      this.notificationService.success('Generation Started', `Initiated generation for ${this.selectedStyles} photo styles. You'll be notified when complete.`);
      
    } catch (error: any) {
      console.error('Generation failed:', error);
      this.notificationService.error('Generation Failed', 'An unexpected error occurred while starting photo generation.');
      this.isGenerating = false;
      this.updateCurrentStep();
    }
  }

  private checkGenerationCompletion(successCount: number, errorCount: number, totalImages: number) {
    console.log(`Generation summary: ${successCount} successful, ${errorCount} failed out of ${totalImages} total`);
    
    if (successCount > 0) {
      // At least some generations succeeded
      this.notificationService.success('Generation Complete', 
        `${successCount} out of ${totalImages} images generated successfully! Check your gallery for results.`);
      
      // Load updated images to show in results
      this.loadUserImages();
      
      // Generation complete, stay in step 2 to show results
      this.isGenerating = false;
      this.updateCurrentStep();
    } else {
      // All generations failed
      this.notificationService.error('Generation Failed', 'All photo generation attempts failed. Please try again.');
      this.isGenerating = false;
      this.updateCurrentStep();
    }
  }

  async generateBasicPhoto() {
    if (!this.creditsInfo || this.creditsInfo.availableCredits <= 0) {
      this.notificationService.creditsExhausted();
      return;
    }

    if (!this.userProfile?.gender) {
      this.notificationService.warning('Profile Incomplete', 'Please update your profile with gender information for better generation results.');
      return;
    }

    this.isGeneratingBasic = true;
    
    try {
      const freeRequest = {
        gender: this.userProfile.gender,
        userInfo: {
          gender: this.userProfile.gender,
          ethnicity: this.userProfile.ethnicity
        }
      };

      this.replicateService.generateBasicImage(freeRequest).subscribe({
        next: (response) => {
          if (response.success) {
            this.creditsInfo!.availableCredits = response.data.creditsRemaining;
            this.notificationService.generationSuccess(response.data.creditsRemaining);
          } else {
            this.notificationService.generationError('Failed to start generation process.');
          }
        },
        error: (error) => {
          console.error('Basic generation failed:', error);
          const errorMessage = error.error?.message || error.message || 'Unknown error occurred';
          this.notificationService.generationError(errorMessage);
        },
        complete: () => {
          this.isGeneratingBasic = false;
        }
      });
      
    } catch (error: any) {
      console.error('Free generation failed:', error);
      this.notificationService.generationError('An unexpected error occurred during generation.');
      this.isGeneratingBasic = false;
    }
  }

  // Photo Results Methods
  downloadPhoto(photo: GeneratedPhoto) {
    const link = document.createElement('a');
    link.href = photo.url;
    link.download = `${photo.style.toLowerCase().replace(/\s+/g, '-')}-photo-${photo.id}.png`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  }

  sharePhoto(photo: GeneratedPhoto) {
    if (navigator.share) {
      navigator.share({
        title: `${photo.style} Profile Photo`,
        text: `Check out my AI-generated ${photo.style} profile photo!`,
        url: photo.url
      });
    } else {
      // Fallback: copy URL to clipboard
      navigator.clipboard.writeText(photo.url).then(() => {
        this.notificationService.success('Link Copied', 'Photo URL copied to clipboard!');
      });
    }
  }

  async downloadAll() {
    if (this.generatedPhotos.length === 0) {
      this.notificationService.warning('No Photos', 'No generated photos to download.');
      return;
    }

    this.isDownloadingZip = true;

    try {
      // Create new JSZip instance
      const zip = new JSZip();
      
      this.notificationService.info('Preparing Download', 'Creating ZIP file with all your photos...');
      
      // Fetch each image and add to ZIP
      const downloadPromises = this.generatedPhotos.map(async (photo, index) => {
        try {
          const response = await fetch(photo.url);
          const blob = await response.blob();
          const filename = `${photo.style.toLowerCase().replace(/\s+/g, '-')}-photo-${index + 1}.png`;
          zip.file(filename, blob);
          return true;
        } catch (error) {
          console.error(`Failed to download photo ${photo.id}:`, error);
          return false;
        }
      });
      
      const results = await Promise.all(downloadPromises);
      const successCount = results.filter(r => r).length;
      
      if (successCount === 0) {
        this.notificationService.error('Download Failed', 'Failed to download any photos. Please try again.');
        return;
      }
      
      // Generate ZIP file
      const zipBlob = await zip.generateAsync({type: 'blob'});
      
      // Create download link
      const link = document.createElement('a');
      link.href = URL.createObjectURL(zipBlob);
      link.download = `ai-profile-photos-${new Date().toISOString().split('T')[0]}.zip`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      
      // Clean up object URL
      URL.revokeObjectURL(link.href);
      
      this.notificationService.success('Download Complete', 
        `Successfully downloaded ${successCount} of ${this.generatedPhotos.length} photos as ZIP file.`);
        
    } catch (error) {
      console.error('ZIP download failed:', error);
      this.notificationService.error('Download Failed', 
        'Failed to create ZIP file. Falling back to individual downloads.');
      
      // Fallback to individual downloads with delay
      this.downloadAllIndividually();
    } finally {
      this.isDownloadingZip = false;
    }
  }
  
  private downloadAllIndividually() {
    this.generatedPhotos.forEach((photo, index) => {
      setTimeout(() => {
        this.downloadPhoto(photo);
      }, index * 1000); // 1 second delay between downloads to avoid browser blocking
    });
  }

  // Status Methods
  getStepStatus(step: number): string {
    if (step < this.currentStep) return 'completed';
    if (step === this.currentStep) return 'active';
    return 'pending';
  }

  getStepStatusText(step: number): string {
    switch (step) {
      case 1:
        return this.uploadedImages > 0 ? 'Completed' : 'Upload Selfies';
      case 2:
        if (this.generatedPhotos.length > 0) return 'Completed';
        if (this.isGenerating) return 'Generating...';
        if (this.modelStatus === 'training') return 'Training Model...';
        if (this.modelStatus === 'trained') {
          return this.selectedStyles > 0 ? 'Ready to Generate' : 'Choose Styles';
        }
        return this.selectedStyles > 0 ? 'Ready to Start' : 'Choose Styles';
      default:
        return 'Pending';
    }
  }


  loadActiveStyles() {
    this.isLoadingStyles = true;
    this.styleService.getActiveStyles().subscribe({
      next: (response) => {
        if (response.success && response.data.length > 0) {
          // Map backend styles to UI style options
          this.availableStyles = response.data.map((style: Style) => ({
            id: style.id.toString(),
            name: style.name,
            description: style.description,
            previewUrl: this.getStylePreviewUrl(style.name),
            selected: false
          }));

          // Load user's previously selected styles
          this.loadUserSelectedStyles();
        } else {
          // Fallback to predefined styles if backend has none
          this.loadFallbackStyles();
        }
      },
      error: (error) => {
        console.error('Failed to load styles:', error);
        // Load fallback styles on error
        this.loadFallbackStyles();
      },
      complete: () => {
        this.isLoadingStyles = false;
      }
    });
  }


  loadFallbackStyles() {
    console.log('Loading fallback styles');
    // Use the same styles as the default initialization by calling initializeStyles
    this.initializeStyles();
  }

  onImageError(event: Event) {
    const img = event.target as HTMLImageElement;
    img.style.display = 'none';
  }

  getStylePreviewUrl(styleName: string): string {
    const styleMap: { [key: string]: string } = {
      // Professional & Career Styles
      'corporate': 'https://images.unsplash.com/photo-1556157382-97eda2d62296?w=150&h=150&fit=crop&crop=face',
      'executive': 'https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=150&h=150&fit=crop&crop=face',
      'consultant': 'https://images.unsplash.com/photo-1506794778202-cad84cf45f1d?w=150&h=150&fit=crop&crop=face',
      'linkedin': 'https://images.unsplash.com/photo-1519085360753-af0119f7cbe7?w=150&h=150&fit=crop&crop=face',
      'legal': 'https://images.unsplash.com/photo-1573496359142-b8d87734a5a2?w=150&h=150&fit=crop&crop=face',
      'medical': 'https://images.unsplash.com/photo-1500648767791-00dcc994a43e?w=150&h=150&fit=crop&crop=face',
      'author': 'https://images.unsplash.com/photo-1560250097-0b93528c311a?w=150&h=150&fit=crop&crop=face',
      
      // Modern Entrepreneur & Tech Styles
      'entrepreneur': 'https://images.unsplash.com/photo-1492562080023-ab3db95bfbce?w=150&h=150&fit=crop&crop=face',
      'startup': 'https://images.unsplash.com/photo-1463453091185-61582044d556?w=150&h=150&fit=crop&crop=face',
      'tech professional': 'https://images.unsplash.com/photo-1582750433449-648ed127bb54?w=150&h=150&fit=crop&crop=face',
      'influencer': 'https://images.unsplash.com/photo-1544005313-94ddf0286df2?w=150&h=150&fit=crop&crop=face',
      'digital nomad': 'https://images.unsplash.com/photo-1528892952291-009c663ce843?w=150&h=150&fit=crop&crop=face',
      
      // Creative, Lifestyle & Expressive Styles
      'creative': 'https://images.unsplash.com/photo-1521119989659-a83eee488004?w=150&h=150&fit=crop&crop=face',
      'casual': 'https://images.unsplash.com/photo-1524504388940-b1c1722653e1?w=150&h=150&fit=crop&crop=face',
      'artistic': 'https://images.unsplash.com/photo-1488426862026-3ee34a7d66df?w=150&h=150&fit=crop&crop=face',
      'edgy/urban': 'https://images.unsplash.com/photo-1531927557220-a9e23c1e4794?w=150&h=150&fit=crop&crop=face',
      'glamour': 'https://images.unsplash.com/photo-1515886657613-9f3515b0c78f?w=150&h=150&fit=crop&crop=face',
      
      // Lifestyle & Identity-Focused Styles
      'academic': 'https://images.unsplash.com/photo-1517841905240-472988babdf9?w=150&h=150&fit=crop&crop=face',
      'fitness': 'https://images.unsplash.com/photo-1531891437562-4301cf35b7e4?w=150&h=150&fit=crop&crop=face',
      'spiritual': 'https://images.unsplash.com/photo-1507591064344-4c6ce005b128?w=150&h=150&fit=crop&crop=face'
    };

    const key = styleName.toLowerCase();
    return styleMap[key] || 'https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=150&h=150&fit=crop&crop=face';
  }

  loadUserSelectedStyles() {
    this.styleService.getUserSelectedStyles().subscribe({
      next: (response) => {
        if (response.success) {
          const selectedStyleIds = response.data.map(style => style.id.toString());
          this.availableStyles.forEach(style => {
            style.selected = selectedStyleIds.includes(style.id);
          });
          this.updateSelectedStyles();
        }
      },
      error: (error) => {
        console.error('Failed to load user selected styles:', error);
      }
    });
  }

  checkModelStatusAndTraining() {
    // First check if model exists on Replicate, then check training status
    this.profileService.checkModelStatus().subscribe({
      next: (response) => {
        console.log('Model status check:', response);
        if (response.success) {
          const { modelExists, modelStatus } = response.data;
          
          if (!modelExists && modelStatus === 'deleted') {
            // Model was deleted from Replicate and cleared from database
            console.log('Model was deleted from Replicate, updating UI');
            this.modelStatus = 'Not Started';
            this.userProfile = this.userProfile || {} as UserProfile;
            this.userProfile.trainedModelId = undefined;
            
            // Show notification to user
            this.notificationService.warning('Model Deleted', 'Your trained model was deleted from Replicate and has been cleared from your account.');
          } else if (modelExists && modelStatus === 'active') {
            console.log('Model exists on Replicate, checking training status');
          }
        }
        
        // Always check training status after model status check
        this.checkTrainingStatus();
      },
      error: (error) => {
        console.error('Model status check failed:', error);
        // Fallback to regular training status check
        this.checkTrainingStatus();
      }
    });
  }

  checkTrainingStatus() {
    this.fileUploadService.getTrainingStatus().subscribe({
      next: (response) => {
        console.log('Training status:', response);
        this.uploadedImages = response.totalUploadedImages || 0;
        
        // Set training ZIP path if available
        if (response.latestZipFile) {
          this.trainingZipPath = response.latestZipFile;
        }
        
        // Priority check: if model is trained, set status to 'trained'
        if (response.hasTrainedModel) {
          this.userProfile = this.userProfile || {} as UserProfile;
          this.userProfile.trainedModelId = response.trainedModelId;
          this.modelStatus = 'trained';
          console.log('Model is trained, status set to: trained');
        } else {
          // Only use API status if model is not trained
          this.modelStatus = response.status || 'Not Started';
          console.log('Model not trained, status set to:', this.modelStatus);
        }
        
        this.updateCurrentStep();
      },
      error: (error) => {
        console.error('Failed to check training status:', error);
        this.modelStatus = 'Error';
      }
    });
  }

  // Theme Methods
  toggleTheme() {
    this.themeService.toggleTheme();
  }

  // Auth Methods
  logout() {
    this.authService.logout();
    this.router.navigate(['/login']);
  }

  // Gallery Methods
  async loadUserImages() {
    this.isLoadingImages = true;
    try {
      const response = await this.fileUploadService.getUserImages().toPromise();
      if (response) {
        console.log('User images response:', response);
        
        // Update dashboard stats
        this.uploadedImages = response.originalUploads || 0;
        
        // Extract uploaded image thumbnails for step 1 display
        this.uploadedImageThumbnails = response.images
          .filter(img => img.isOriginalUpload && !img.isGenerated)
          .map(img => ({
            id: img.id, // keep as number
            url: img.originalImageUrl,
            fileName: img.originalImageUrl.split('/').pop() || 'image'
          }));
        
        // Map images for gallery
        this.galleryImages = response.images.map((img: ProcessedImage) => ({
          id: img.id,
          url: img.processedImageUrl || img.originalImageUrl,
          thumbnailUrl: img.originalImageUrl,
          title: img.isGenerated ? `${img.style} Photo` : 'Uploaded Photo',
          description: img.isGenerated ? `Generated ${img.style} style profile photo` : 'Original uploaded image',
          style: img.style || 'original',
          createdAt: new Date(img.createdAt),
          status: 'completed' as const,
          type: img.isGenerated ? 'generated' as const : 'original' as const,
          downloadUrl: img.processedImageUrl || img.originalImageUrl
        }));
        
        // Extract generated photos for dashboard results
        this.generatedPhotos = response.images
          .filter(img => img.isGenerated)
          .map((img: ProcessedImage) => ({
            id: img.id.toString(),
            url: img.processedImageUrl || img.originalImageUrl,
            style: img.style || 'Unknown',
            createdAt: new Date(img.createdAt)
          }));
        
        if (this.galleryImages.length === 0) {
          this.notificationService.info('No Images Yet', 'Upload some selfies and generate photos to see them here!');
        }
        
        this.updateCurrentStep();
      } else {
        this.notificationService.error('Images Load Failed', 'Failed to load your images.');
      }
    } catch (error: any) {
      console.error('Failed to load user images:', error);
      const errorMessage = error.error?.message || error.message || 'Unknown error';
      this.notificationService.error('Images Unavailable', `Unable to load images: ${errorMessage}`);
    } finally {
      this.isLoadingImages = false;
    }
  }

  onImageClick(image: GalleryImage) {
    // Open image in full-screen viewer or modal
    window.open(image.url, '_blank');
  }

  onImageDownload(image: GalleryImage) {
    const link = document.createElement('a');
    link.href = image.downloadUrl || image.url;
    link.download = `${image.title.toLowerCase().replace(/\s+/g, '-')}-${image.id}.jpg`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  }

  onImageShare(image: GalleryImage) {
    if (navigator.share) {
      navigator.share({
        title: image.title,
        text: image.description || 'Check out my AI-generated profile photo!',
        url: image.url
      });
    } else {
      // Fallback: copy to clipboard
      navigator.clipboard.writeText(image.url).then(() => {
        // Show toast notification (you can add a toast service)
        console.log('Image URL copied to clipboard');
      });
    }
  }

  onImageDelete(image: GalleryImage) {
    if (confirm(`Are you sure you want to delete "${image.title}"?`)) {
      this.fileUploadService.deleteImage(image.id).subscribe({
        next: (response) => {
          if (response.success) {
            this.galleryImages = this.galleryImages.filter(img => img.id !== image.id);
            this.notificationService.success('Image Deleted', `Successfully deleted "${image.title}".`);
          } else {
            this.notificationService.error('Delete Failed', 'Failed to delete the image.');
          }
        },
        error: (error: any) => {
          console.error('Failed to delete image:', error);
          const errorMessage = error.error?.message || error.message || 'Unknown error';
          this.notificationService.error('Delete Error', `Failed to delete image: ${errorMessage}`);
        }
      });
    }
  }

  deleteUploadedImage(thumb: {id: number; url: string; fileName: string}, index: number) {
    if (confirm(`Are you sure you want to delete "${thumb.fileName}"?`)) {
      this.fileUploadService.deleteImage(thumb.id).subscribe({
        next: (response) => {
          if (response.success) {
            // Remove from thumbnails array
            this.uploadedImageThumbnails.splice(index, 1);
            // Update uploaded images count
            this.uploadedImages = this.uploadedImageThumbnails.length;
            // Also remove from gallery images if present
            this.galleryImages = this.galleryImages.filter(img => img.id !== thumb.id);
            this.notificationService.success('Image Deleted', `Successfully deleted "${thumb.fileName}".`);
            
            // Update current step if no images left
            this.updateCurrentStep();
          } else {
            this.notificationService.error('Delete Failed', 'Failed to delete the image.');
          }
        },
        error: (error: any) => {
          console.error('Failed to delete uploaded image:', error);
          const errorMessage = error.error?.message || error.message || 'Unknown error';
          this.notificationService.error('Delete Error', `Failed to delete image: ${errorMessage}`);
        }
      });
    }
  }

  onBulkDownload(images: GalleryImage[]) {
    images.forEach(image => {
      this.onImageDownload(image);
    });
  }

  toggleGalleryView() {
    this.showGallery = !this.showGallery;
    if (this.showGallery) {
      this.loadUserImages();
    }
  }

  goToEnhancement() {
    this.router.navigate(['/enhance']);
  }

  // Credit System Methods
  loadCreditStatus() {
    this.creditService.getCreditStatus().subscribe({
      next: (response) => {
        if (response.success) {
          this.userCreditStatus = response.data;
          console.log('Credit status loaded:', this.userCreditStatus);
        }
      },
      error: (error) => {
        console.error('Failed to load credit status:', error);
        // Don't redirect on error - just show the dashboard without credit features
      }
    });
  }


  isPremiumWorkflow(): boolean {
    return (this.userCreditStatus?.purchasedCredits || 0) > 0;
  }

  ngOnDestroy() {
    // Clean up training status polling to prevent memory leaks
    if (this.trainingStatusInterval) {
      clearInterval(this.trainingStatusInterval);
      this.trainingStatusInterval = null;
    }
  }
}

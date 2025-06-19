import { Component, OnInit, ViewChild, ElementRef } from '@angular/core';
import { Router, ActivatedRoute } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuthService } from '../services/auth.service';
import { ThemeService } from '../services/theme.service';
import { PhotoGalleryComponent, GalleryImage } from '../components/photo-gallery/photo-gallery.component';
import { FileUploadService, ProcessedImage } from '../services/file-upload.service';
import { ProfileService, UserProfile } from '../services/profile.service';
import { ReplicateService, CreditsInfo } from '../services/replicate.service';
import { StyleService, Style } from '../services/style.service';
import { NotificationService } from '../services/notification.service';

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
  imports: [CommonModule, PhotoGalleryComponent],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.sass'
})
export class DashboardComponent implements OnInit {
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

  // Style Selection
  availableStyles: StyleOption[] = [
    {
      id: 'professional',
      name: 'Professional',
      description: 'Clean, corporate headshots perfect for LinkedIn',
      previewUrl: '/assets/styles/professional.jpg',
      selected: false
    },
    {
      id: 'creative',
      name: 'Creative',
      description: 'Artistic and unique styles for creative professionals',
      previewUrl: '/assets/styles/creative.jpg',
      selected: false
    },
    {
      id: 'casual',
      name: 'Casual',
      description: 'Relaxed, approachable photos for social media',
      previewUrl: '/assets/styles/casual.jpg',
      selected: false
    },
    {
      id: 'formal',
      name: 'Formal',
      description: 'Elegant, sophisticated portraits',
      previewUrl: '/assets/styles/formal.jpg',
      selected: false
    },
    {
      id: 'outdoor',
      name: 'Outdoor',
      description: 'Natural lighting with outdoor backgrounds',
      previewUrl: '/assets/styles/outdoor.jpg',
      selected: false
    },
    {
      id: 'studio',
      name: 'Studio',
      description: 'Classic studio lighting and backgrounds',
      previewUrl: '/assets/styles/studio.jpg',
      selected: false
    }
  ];

  // Generation
  isGenerating: boolean = false;
  isGeneratingBasic: boolean = false;


  // Photo Gallery
  galleryImages: GalleryImage[] = [];
  showGallery: boolean = false;

  constructor(
    private authService: AuthService,
    private router: Router,
    private route: ActivatedRoute,
    public themeService: ThemeService,
    private fileUploadService: FileUploadService,
    private profileService: ProfileService,
    private replicateService: ReplicateService,
    private styleService: StyleService,
    private notificationService: NotificationService
  ) {}

  ngOnInit() {
    console.log('Dashboard ngOnInit');
    
    // Check authentication first
    if (!this.authService.isAuthenticated()) {
      console.log('Not authenticated, redirecting to login');
      this.router.navigate(['/login']);
      return;
    }
    
    console.log('User is authenticated, loading dashboard data');
    this.loadUserInfo();
    this.loadDashboardData();
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
    this.checkTrainingStatus();
    this.updateCurrentStep();
    // Note: Styles are loaded on-demand when user reaches step 3 (style selection)
  }

  updateCurrentStep() {
    // Get uploaded images count from user profile or file service
    if (this.userProfile?.id) {
      this.uploadedImages = this.userProfile.id ? 1 : 0; // Simplified - should check actual uploaded images
    }

    if (this.uploadedImages === 0) {
      this.currentStep = 1;
    } else if (this.modelStatus !== 'trained' && this.modelStatus !== 'succeeded') {
      this.currentStep = 2;
    } else if (this.selectedStyles === 0) {
      this.currentStep = 3;
      // Load styles on-demand when user reaches style selection step
      if (this.availableStyles.length === 0) {
        this.loadActiveStyles();
      }
    } else {
      this.currentStep = 4;
    }
  }

  updateSelectedStyles() {
    this.selectedStyles = this.availableStyles.filter(s => s.selected).length;
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

  addFiles(files: File[]) {
    const validFiles = files.filter(file => {
      return file.type.startsWith('image/') && file.size <= 5 * 1024 * 1024; // 5MB limit
    });

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

      this.fileUploadService.uploadImages(this.selectedFiles, profileData).subscribe({
        next: (result) => {
          this.uploadProgress = result.progress;
          
          if (result.response) {
            // Upload completed successfully
            this.uploadedImages = result.response.uploadedFiles.length;
            this.selectedFiles = [];
            
            // Update user profile if we got profile ID back
            if (result.response.profileId && this.userProfile) {
              this.userProfile.id = result.response.profileId;
            }
            
            // Check if training ZIP was created
            if (result.response.zipCreated) {
              this.notificationService.success('Upload Complete', 
                `${this.uploadedImages} images uploaded successfully. Training package prepared!`);
            } else {
              this.notificationService.success('Upload Complete', 
                `${this.uploadedImages} images uploaded successfully.`);
            }
            
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
  toggleStyle(style: StyleOption) {
    const selectedCount = this.availableStyles.filter(s => s.selected).length;
    
    if (!style.selected && selectedCount >= 10) {
      alert('You can select a maximum of 10 styles.');
      return;
    }
    
    style.selected = !style.selected;
    this.updateSelectedStyles();
    this.saveStyleSelection();
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

  async generatePhotos() {
    if (!this.userProfile?.trainedModelId) {
      this.notificationService.warning('Model Not Ready', 'Please wait for model training to complete before generating photos.');
      return;
    }

    if (this.selectedStyles === 0) {
      this.notificationService.warning('No Styles Selected', 'Please select at least one style before generating photos.');
      return;
    }

    this.isGenerating = true;
    
    const selectedStyleNames = this.availableStyles
      .filter(style => style.selected)
      .map(style => style.name);

    try {
      let successCount = 0;
      let errorCount = 0;
      
      // Generate photos for each selected style
      for (const styleName of selectedStyleNames) {
        const generateRequest = {
          trainedModelVersion: this.userProfile.trainedModelId,
          userId: this.userProfile.userId,
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
            } else {
              errorCount++;
              this.notificationService.error('Generation Failed', `Failed to start ${styleName} style generation.`);
            }
          },
          error: (error) => {
            console.error(`Failed to generate ${styleName} style:`, error);
            errorCount++;
            const errorMessage = error.error?.message || error.message || 'Unknown error';
            this.notificationService.error('Generation Error', `${styleName} style: ${errorMessage}`);
          }
        });
      }
      
      this.notificationService.success('Generation Started', `Initiated generation for ${this.selectedStyles} photo styles. You'll be notified when complete.`);
      this.updateCurrentStep();
      
    } catch (error: any) {
      console.error('Generation failed:', error);
      this.notificationService.error('Generation Failed', 'An unexpected error occurred while starting photo generation.');
    } finally {
      this.isGenerating = false;
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
    // TODO: Implement download
    console.log('Downloading photo:', photo);
  }

  sharePhoto(photo: GeneratedPhoto) {
    // TODO: Implement sharing
    console.log('Sharing photo:', photo);
  }

  downloadAll() {
    // TODO: Implement bulk download
    console.log('Downloading all photos');
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
        return this.modelStatus === 'trained' ? 'Completed' : 
               this.modelStatus === 'training' ? 'Training...' : 'Pending';
      case 3:
        return this.selectedStyles > 0 ? 'Completed' : 'Choose Styles';
      case 4:
        return this.generatedPhotos.length > 0 ? 'Completed' : 'Pending';
      default:
        return 'Pending';
    }
  }


  loadActiveStyles() {
    this.isLoadingStyles = true;
    this.styleService.getActiveStyles().subscribe({
      next: (response) => {
        if (response.success) {
          // Map backend styles to UI style options
          this.availableStyles = response.data.map((style: Style) => ({
            id: style.id.toString(),
            name: style.name,
            description: style.description,
            previewUrl: `/assets/styles/${style.name.toLowerCase()}.jpg`,
            selected: false
          }));

          // Load user's previously selected styles
          this.loadUserSelectedStyles();
        } else {
          this.notificationService.error('Styles Load Failed', 'Failed to load available photo styles.');
        }
      },
      error: (error) => {
        console.error('Failed to load styles:', error);
        this.notificationService.error('Styles Unavailable', 'Unable to load photo styles. Please refresh the page and try again.');
      },
      complete: () => {
        this.isLoadingStyles = false;
      }
    });
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

  checkTrainingStatus() {
    this.fileUploadService.getTrainingStatus().subscribe({
      next: (response) => {
        console.log('Training status:', response);
        this.modelStatus = response.status || 'Not Started';
        this.uploadedImages = response.totalUploadedImages || 0;
        
        if (response.hasTrainedModel) {
          this.userProfile = this.userProfile || {} as UserProfile;
          this.userProfile.trainedModelId = response.trainedModelId;
          this.modelStatus = 'trained';
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
}

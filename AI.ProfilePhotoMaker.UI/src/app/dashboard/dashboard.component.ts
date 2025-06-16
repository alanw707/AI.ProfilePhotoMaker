import { Component, OnInit, ViewChild, ElementRef } from '@angular/core';
import { Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuthService } from '../services/auth.service';

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

interface ActivityItem {
  icon: string;
  message: string;
  time: string;
}

@Component({
  selector: 'app-dashboard',
  imports: [CommonModule],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.sass'
})
export class DashboardComponent implements OnInit {
  @ViewChild('fileInput') fileInput!: ElementRef<HTMLInputElement>;

  // User Info
  userName: string = '';
  userEmail: string = '';

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

  // Recent Activity
  recentActivity: ActivityItem[] = [];

  constructor(
    private authService: AuthService,
    private router: Router
  ) {}

  ngOnInit() {
    this.loadUserInfo();
    this.loadDashboardData();
    this.updateSelectedStyles();
  }

  loadUserInfo() {
    // Get user info from auth service or API
    this.userName = 'Alan Wang'; // TODO: Get from API
    this.userEmail = 'alanw707@gmail.com'; // TODO: Get from API
  }

  loadDashboardData() {
    // TODO: Load actual data from API
    this.updateCurrentStep();
    this.loadRecentActivity();
  }

  updateCurrentStep() {
    if (this.uploadedImages === 0) {
      this.currentStep = 1;
    } else if (this.modelStatus !== 'trained') {
      this.currentStep = 2;
    } else if (this.selectedStyles === 0) {
      this.currentStep = 3;
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
      return file.type.startsWith('image/') && file.size <= 10 * 1024 * 1024; // 10MB limit
    });

    // Limit to 10 total files
    const remainingSlots = 10 - this.selectedFiles.length;
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
    if (this.selectedFiles.length === 0) return;

    this.isUploading = true;
    try {
      // TODO: Implement actual upload to API
      console.log('Uploading files:', this.selectedFiles);
      
      // Simulate upload
      await new Promise(resolve => setTimeout(resolve, 2000));
      
      this.uploadedImages = this.selectedFiles.length;
      this.selectedFiles = [];
      this.addActivity('📤', `Uploaded ${this.uploadedImages} selfies`, 'Just now');
      this.updateCurrentStep();
      
    } catch (error) {
      console.error('Upload failed:', error);
    } finally {
      this.isUploading = false;
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
  }

  async generatePhotos() {
    this.isGenerating = true;
    try {
      // TODO: Implement actual generation API call
      console.log('Generating photos for selected styles');
      
      // Simulate generation
      await new Promise(resolve => setTimeout(resolve, 3000));
      
      this.addActivity('⚡', `Started generating ${this.selectedStyles} photo styles`, 'Just now');
      this.updateCurrentStep();
      
    } catch (error) {
      console.error('Generation failed:', error);
    } finally {
      this.isGenerating = false;
    }
  }

  // Photo Results Methods
  downloadPhoto(photo: GeneratedPhoto) {
    // TODO: Implement download
    console.log('Downloading photo:', photo);
    this.addActivity('⬇️', `Downloaded ${photo.style} photo`, 'Just now');
  }

  sharePhoto(photo: GeneratedPhoto) {
    // TODO: Implement sharing
    console.log('Sharing photo:', photo);
  }

  downloadAll() {
    // TODO: Implement bulk download
    console.log('Downloading all photos');
    this.addActivity('📦', `Downloaded all ${this.generatedPhotos.length} photos`, 'Just now');
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

  // Activity Methods
  addActivity(icon: string, message: string, time: string) {
    this.recentActivity.unshift({ icon, message, time });
    if (this.recentActivity.length > 5) {
      this.recentActivity.pop();
    }
  }

  loadRecentActivity() {
    // TODO: Load from API
    this.recentActivity = [
      { icon: '👋', message: 'Welcome to AI Profile Photo Maker!', time: '2 minutes ago' }
    ];
  }

  // Auth Methods
  logout() {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}

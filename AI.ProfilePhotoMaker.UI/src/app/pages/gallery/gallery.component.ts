import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { HeaderNavigationComponent } from '../../shared/header-navigation/header-navigation.component';
import { PhotoGalleryComponent, GalleryImage } from '../../components/photo-gallery/photo-gallery.component';
import { FileUploadService, ProcessedImage } from '../../services/file-upload.service';

@Component({
  selector: 'app-gallery',
  standalone: true,
  imports: [CommonModule, RouterModule, PhotoGalleryComponent, HeaderNavigationComponent],
  templateUrl: './gallery.component.html',
  styleUrls: ['./gallery.component.sass']
})
export class GalleryComponent implements OnInit {
  galleryImages: GalleryImage[] = [];
  isLoading = false;

  constructor(
    private authService: AuthService,
    private router: Router,
    private fileUploadService: FileUploadService
  ) {}

  ngOnInit() {
    if (!this.authService.isAuthenticated()) {
      this.router.navigate(['/login']);
      return;
    }
    
    this.loadImages();
  }

  async loadImages() {
    this.isLoading = true;
    try {
      const response = await this.fileUploadService.getUserImages().toPromise();
      if (response) {
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
      }
    } catch (error) {
      console.error('Failed to load images:', error);
    } finally {
      this.isLoading = false;
    }
  }

  onImageClick(image: GalleryImage) {
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
      navigator.clipboard.writeText(image.url);
    }
  }

  onImageDelete(image: GalleryImage) {
    if (confirm(`Are you sure you want to delete "${image.title}"?`)) {
      this.fileUploadService.deleteImage(image.id).subscribe({
        next: (response) => {
          if (response.success) {
            this.galleryImages = this.galleryImages.filter(img => img.id !== image.id);
          }
        },
        error: (error) => {
          console.error('Failed to delete image:', error);
        }
      });
    }
  }

  onBulkDownload(images: GalleryImage[]) {
    images.forEach(image => {
      this.onImageDownload(image);
    });
  }
}

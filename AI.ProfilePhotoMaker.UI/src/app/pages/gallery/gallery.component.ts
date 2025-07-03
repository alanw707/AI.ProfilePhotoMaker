import { Component, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { HeaderNavigationComponent } from '../../shared/header-navigation/header-navigation.component';
import { PhotoGalleryComponent, GalleryImage } from '../../components/photo-gallery/photo-gallery.component';
import { FileUploadService, ProcessedImage } from '../../services/file-upload.service';
import JSZip from 'jszip';

@Component({
  selector: 'app-gallery',
  standalone: true,
  imports: [CommonModule, RouterModule, PhotoGalleryComponent, HeaderNavigationComponent],
  templateUrl: './gallery.component.html',
  styleUrls: ['./gallery.component.sass']
})
export class GalleryComponent implements OnInit {
  @ViewChild('photoGallery') photoGallery!: PhotoGalleryComponent;
  
  galleryImages: GalleryImage[] = [];
  isLoading = false;
  isDownloading = false;
  downloadProgress = 0;
  private hasRunInitialRepair = false;

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

  async loadImages(forceRefresh: boolean = false) {
    this.isLoading = true;
    try {
      // Run image database repair on first load only to sync filesystem with database
      if (!this.hasRunInitialRepair && !forceRefresh) {
        console.log('🔧 Running initial image database repair...');
        try {
          const repairResponse = await this.fileUploadService.repairImageDatabase().toPromise();
          if (repairResponse?.success) {
            console.log('✅ Image repair completed:', repairResponse.message);
          }
        } catch (repairError) {
          console.warn('⚠️ Image repair failed, continuing with normal load:', repairError);
        }
        this.hasRunInitialRepair = true;
      }

      const response = await this.fileUploadService.getUserImages(forceRefresh).toPromise();
      if (response) {
        // Deduplicate images by ID to prevent duplicates in zip downloads
        const uniqueImages = response.images.filter((img, index, array) => 
          array.findIndex(i => i.id === img.id) === index
        );
        
        this.galleryImages = uniqueImages.map((img: ProcessedImage) => ({
          id: img.id,
          url: img.processedImageUrl || img.originalImageUrl,
          thumbnailUrl: img.originalImageUrl,
          title: img.isGenerated ? `${this.formatStyleName(img.style)} Photo` : 'Uploaded Photo',
          description: img.isGenerated ? `Generated ${this.formatStyleName(img.style)} style profile photo` : 'Original uploaded image',
          style: img.style || 'original',
          createdAt: new Date(img.createdAt),
          status: 'completed' as const,
          type: img.isGenerated ? 'generated' as const : 'original' as const,
          downloadUrl: img.processedImageUrl || img.originalImageUrl
        }));
        
        // Log if duplicates were found and removed
        if (uniqueImages.length < response.images.length) {
          console.warn(`🔍 Removed ${response.images.length - uniqueImages.length} duplicate images from display`);
        }
      }
    } catch (error) {
      console.error('Failed to load images:', error);
    } finally {
      this.isLoading = false;
    }
  }

  refreshGallery() {
    this.loadImages(true);
  }

  onImageClick(image: GalleryImage) {
    window.open(image.url, '_blank');
  }



  async testImageAccess(image: GalleryImage) {
    const imageUrl = image.downloadUrl || image.url;
    
    try {
      const response = await fetch(imageUrl, {
        method: 'HEAD',
        mode: 'cors',
        cache: 'no-cache'
      });
      
      return response.ok;
    } catch (error) {
      // Try with img element to test basic accessibility
      return new Promise((resolve) => {
        const img = new Image();
        img.onload = () => resolve(true);
        img.onerror = () => resolve(false);
        img.src = imageUrl;
      });
    }
  }

  async onImageDownload(image: GalleryImage) {
    // Skip enhanced images for now - focus on generated images
    const isEnhancedImage = image.style === 'Background Remover' || image.style === 'Social Media' || image.style === 'Cartoon';
    if (isEnhancedImage) {
      alert(`Enhanced image downloads are not yet implemented. Focusing on regular generated images first.\n\nImage: ${image.title} (${image.style})`);
      return;
    }

    // First test if the image is accessible
    const isAccessible = await this.testImageAccess(image);
    if (!isAccessible) {
      alert('This image appears to be inaccessible. It may have expired or been moved.');
      return;
    }

    try {
      const imageUrl = image.downloadUrl || image.url;
      
      // Try fetch with CORS mode (required for blob downloads)
      const response = await fetch(imageUrl, {
        mode: 'cors',
        cache: 'no-cache',
        credentials: 'omit',
        headers: {
          'ngrok-skip-browser-warning': 'true'
        }
      });
      
      if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
      }
      
      const contentType = response.headers.get('content-type') || '';
      const blob = await response.blob();
      
      // Verify blob is valid before creating download
      if (blob.size === 0) {
        throw new Error('Downloaded file is empty (0 bytes)');
      }
      
      // Check if we received HTML instead of an image
      if (contentType.includes('text/html')) {
        throw new Error('Server returned HTML page instead of image. The image URL may be expired, moved, or require authentication.');
      }
      
      // Additional validation for small files that might be error pages
      if (blob.size < 1000 && contentType.includes('text/')) {
        throw new Error('Server returned text content instead of image - possible error page or expired URL');
      }
      
      // Determine file extension - default to PNG for AI-generated images
      let extension = 'png';
      const finalContentType = contentType || blob.type;
      
      if (finalContentType) {
        if (finalContentType.includes('jpeg') || finalContentType.includes('jpg')) extension = 'jpg';
        else if (finalContentType.includes('png')) extension = 'png';
        else if (finalContentType.includes('webp')) extension = 'webp';
        else if (finalContentType.includes('gif')) extension = 'gif';
      } else {
        // Fallback: get extension from URL
        const urlExtension = imageUrl.split('.').pop()?.toLowerCase();
        if (urlExtension && ['jpg', 'jpeg', 'png', 'webp', 'gif'].includes(urlExtension)) {
          extension = urlExtension === 'jpeg' ? 'jpg' : urlExtension;
        }
      }
      
      const filename = `${image.title.toLowerCase().replace(/\s+/g, '-')}-${image.id}.${extension}`;
      
      // Create object URL for the blob
      const blobUrl = window.URL.createObjectURL(blob);
      
      // Create download link
      const link = document.createElement('a');
      link.href = blobUrl;
      link.download = filename;
      link.style.display = 'none';
      
      // Trigger download
      document.body.appendChild(link);
      link.click();
      
      // Clean up after download
      setTimeout(() => {
        if (document.body.contains(link)) {
          document.body.removeChild(link);
        }
        window.URL.revokeObjectURL(blobUrl);
      }, 100);
      
      
    } catch (error) {
      console.error('❌ Download failed:', error);
      
      const errorMessage = error instanceof Error ? error.message : String(error);
      alert(`Download failed: ${errorMessage}\n\nPlease try the fallback method or right-click the image to save manually.`);
      this.fallbackDownload(image);
    }
  }

  private fallbackDownload(image: GalleryImage) {
    try {
      const imageUrl = image.downloadUrl || image.url;
      
      // Try to determine extension from URL for fallback
      let extension = 'png'; // Default to PNG
      const urlExtension = imageUrl.split('.').pop()?.toLowerCase();
      if (urlExtension && ['jpg', 'jpeg', 'png', 'webp', 'gif'].includes(urlExtension)) {
        extension = urlExtension === 'jpeg' ? 'jpg' : urlExtension;
      }
      
      const filename = `${image.title.toLowerCase().replace(/\s+/g, '-')}-${image.id}.${extension}`;
      
      // Create a more robust fallback download
      const link = document.createElement('a');
      link.href = imageUrl;
      link.download = filename;
      link.target = '_blank';
      link.rel = 'noopener noreferrer';
      
      // Try to force download with Content-Disposition
      link.style.display = 'none';
      link.setAttribute('download', filename);
      
      document.body.appendChild(link);
      link.click();
      
      setTimeout(() => {
        document.body.removeChild(link);
      }, 100);
      
      alert(`Download initiated via fallback method. If the image opens in a new tab instead of downloading, please right-click and select "Save image as..." to save as ${filename}`);
      
    } catch (fallbackError) {
      alert('Download failed completely. Please right-click the image and select "Save image as..." or check if the image URL is accessible.');
    }
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

  async onBulkDownload(images: GalleryImage[]) {
    if (images.length === 0) {
      console.warn('No images selected for download');
      return;
    }

    // Filter out enhanced images for now
    const generatedImages = images.filter(img => {
      const isEnhanced = img.style === 'Background Remover' || img.style === 'Social Media' || img.style === 'Cartoon';
      return !isEnhanced;
    });

    const enhancedCount = images.length - generatedImages.length;
    
    if (enhancedCount > 0) {
      alert(`Skipping ${enhancedCount} enhanced images for now. Enhanced image downloads will be implemented later.\n\nDownloading ${generatedImages.length} generated images.`);
    }

    if (generatedImages.length === 0) {
      alert('No regular generated images selected. Enhanced image downloads are not yet implemented.');
      return;
    }

    if (generatedImages.length === 1) {
      // Single image: use direct download
      await this.onImageDownload(generatedImages[0]);
      // Clear selections after single image download
      if (this.photoGallery) {
        this.photoGallery.clearSelections();
      }
    } else {
      // Multiple images: create zip (clearSelections is handled in createZipDownload finally block)
      await this.createZipDownload(generatedImages);
    }
  }

  private async createZipDownload(images: GalleryImage[]) {
    this.isDownloading = true;
    this.downloadProgress = 0;

    try {
      const zip = new JSZip();
      const imageFolder = zip.folder('profile-photos');

      for (let i = 0; i < images.length; i++) {
        const image = images[i];
        this.downloadProgress = Math.round(((i + 1) / images.length) * 90); // Reserve 10% for zip generation

        try {
          const imageUrl = image.downloadUrl || image.url;
          
          // Fetch image with improved error handling
          const response = await fetch(imageUrl, {
            mode: 'cors',
            cache: 'no-cache',
            credentials: 'omit',
            headers: {
              'ngrok-skip-browser-warning': 'true'
            }
          });

          if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
          }

          const blob = await response.blob();
          
          // Verify blob is valid
          if (blob.size === 0) {
            throw new Error('Downloaded blob is empty');
          }
          
          // Determine file extension - default to PNG for AI-generated images
          let extension = 'png'; // Default to PNG instead of JPG
          const contentType = response.headers.get('content-type') || blob.type;
          
          if (contentType) {
            if (contentType.includes('jpeg') || contentType.includes('jpg')) extension = 'jpg';
            else if (contentType.includes('png')) extension = 'png';
            else if (contentType.includes('webp')) extension = 'webp';
            else if (contentType.includes('gif')) extension = 'gif';
          } else {
            const urlExtension = imageUrl.split('.').pop()?.toLowerCase();
            if (urlExtension && ['jpg', 'jpeg', 'png', 'webp', 'gif'].includes(urlExtension)) {
              extension = urlExtension === 'jpeg' ? 'jpg' : urlExtension;
            }
          }

          const filename = `${image.title.toLowerCase().replace(/\s+/g, '-')}-${image.id}.${extension}`;
          
          // Add image to zip
          if (imageFolder) {
            imageFolder.file(filename, blob);
          } else {
            zip.file(filename, blob);
          }

        } catch (error) {
          // Continue with other images instead of failing completely
        }
      }

      this.downloadProgress = 95;

      // Generate zip file
      const zipBlob = await zip.generateAsync({
        type: 'blob',
        compression: 'DEFLATE',
        compressionOptions: {
          level: 6
        }
      });

      this.downloadProgress = 100;

      // Create download link for zip
      const zipUrl = window.URL.createObjectURL(zipBlob);
      const link = document.createElement('a');
      link.href = zipUrl;
      link.download = `profile-photos-${new Date().toISOString().split('T')[0]}.zip`;
      link.style.display = 'none';

      document.body.appendChild(link);
      link.click();

      setTimeout(() => {
        document.body.removeChild(link);
        window.URL.revokeObjectURL(zipUrl);
      }, 100);


    } catch (error) {
      alert('Failed to create zip file. Please try downloading images individually.');
    } finally {
      this.isDownloading = false;
      this.downloadProgress = 0;
      
      // Clear selections after download completes (success or failure)
      if (this.photoGallery) {
        this.photoGallery.clearSelections();
      }
    }
  }

  formatStyleName(style: string): string {
    if (!style) return '';
    return style
      .replace(/[-_/]/g, ' ')  // Replace dashes, underscores, and slashes with spaces
      .split(' ')
      .map(word => word.charAt(0).toUpperCase() + word.slice(1).toLowerCase())
      .join(' ');
  }
}

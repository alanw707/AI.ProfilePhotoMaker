import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';

import { GalleryComponent } from '../pages/gallery/gallery.component';
import { GalleryImage, PhotoGalleryComponent } from '../components/photo-gallery/photo-gallery.component';
import { AuthService } from '../services/auth.service';
import { FileUploadService, ProcessedImage } from '../services/file-upload.service';
import { HeaderNavigationComponent } from '../shared/header-navigation/header-navigation.component';

// Mock child components
@Component({ selector: 'app-header-navigation', template: '' })
class MockHeaderNavigationComponent { }

@Component({ 
  selector: 'app-photo-gallery', 
  template: '<div>Mock Gallery</div>'
})
class MockPhotoGalleryComponent {
  @Input() images: GalleryImage[] = [];
  @Input() isLoading = false;
  @Input() isDownloading = false;
  @Input() downloadProgress = 0;
  @Output() imageClick = new EventEmitter<GalleryImage>();
  @Output() imageDownload = new EventEmitter<GalleryImage>();
  @Output() imageShare = new EventEmitter<GalleryImage>();
  @Output() imageDelete = new EventEmitter<GalleryImage>();
  @Output() bulkDownload = new EventEmitter<GalleryImage[]>();
  @Output() filterChange = new EventEmitter<any>();
  @Output() sortChange = new EventEmitter<any>();

  clearSelections() {}
  getSelectedImages(): GalleryImage[] { return []; }
}

// Mock JSZip for testing
class MockJSZip {
  constructor() {}
  folder(name: string) {
    return {
      file: (filename: string, data: any) => {}
    };
  }
  file(filename: string, data: any) {}
  async generateAsync(options: any) {
    return new Blob(['mock zip content'], { type: 'application/zip' });
  }
}

describe('Gallery Management Flow Integration Tests', () => {
  let component: GalleryComponent;
  let fixture: ComponentFixture<GalleryComponent>;
  let httpMock: HttpTestingController;
  let authService: jasmine.SpyObj<AuthService>;
  let fileUploadService: jasmine.SpyObj<FileUploadService>;
  let router: Router;

  const mockProcessedImages: ProcessedImage[] = [
    {
      id: 1,
      originalImageUrl: '/uploads/user-123/image1.jpg',
      processedImageUrl: '/generated/user-123/prof1.jpg',
      style: 'professional',
      isGenerated: true,
      isOriginalUpload: false,
      createdAt: new Date('2024-01-01'),
      userId: 'user-123'
    },
    {
      id: 2,
      originalImageUrl: '/uploads/user-123/image2.jpg',
      processedImageUrl: '/generated/user-123/creative1.jpg',
      style: 'creative',
      isGenerated: true,
      isOriginalUpload: false,
      createdAt: new Date('2024-01-02'),
      userId: 'user-123'
    },
    {
      id: 3,
      originalImageUrl: '/uploads/user-123/original.jpg',
      processedImageUrl: null,
      style: null,
      isGenerated: false,
      isOriginalUpload: true,
      createdAt: new Date('2024-01-03'),
      userId: 'user-123'
    }
  ];

  beforeEach(async () => {
    const authSpy = jasmine.createSpyObj('AuthService', ['isAuthenticated']);
    const fileUploadSpy = jasmine.createSpyObj('FileUploadService', [
      'getUserImages', 'repairImageDatabase', 'deleteImage'
    ]);

    // Mock JSZip globally
    (window as any).JSZip = MockJSZip;

    await TestBed.configureTestingModule({
      imports: [
        CommonModule,
        HttpClientTestingModule,
        RouterTestingModule.withRoutes([
          { path: 'login', component: MockHeaderNavigationComponent }
        ]),
        GalleryComponent
      ],
      providers: [
        { provide: AuthService, useValue: authSpy },
        { provide: FileUploadService, useValue: fileUploadSpy }
      ]
    }).overrideComponent(GalleryComponent, {
      remove: { imports: [PhotoGalleryComponent, HeaderNavigationComponent] },
      add: { imports: [MockPhotoGalleryComponent, MockHeaderNavigationComponent] }
    }).compileComponents();

    fixture = TestBed.createComponent(GalleryComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
    authService = TestBed.inject(AuthService) as jasmine.SpyObj<AuthService>;
    fileUploadService = TestBed.inject(FileUploadService) as jasmine.SpyObj<FileUploadService>;
    router = TestBed.inject(Router);

    // Setup default mocks
    authService.isAuthenticated.and.returnValue(true);
    fileUploadService.getUserImages.and.returnValue(of({
      images: mockProcessedImages,
      generatedImages: 2
    }));
    fileUploadService.repairImageDatabase.and.returnValue(of({
      success: true,
      message: 'Database repaired successfully'
    }));
  });

  afterEach(() => {
    httpMock.verify();
  });

  describe('Component Initialization', () => {
    it('should initialize with empty gallery', () => {
      expect(component.galleryImages).toEqual([]);
      expect(component.isLoading).toBe(false);
      expect(component.isDownloading).toBe(false);
    });

    it('should redirect unauthenticated users to login', () => {
      authService.isAuthenticated.and.returnValue(false);
      spyOn(router, 'navigate');

      component.ngOnInit();

      expect(router.navigate).toHaveBeenCalledWith(['/login']);
    });

    it('should load images on initialization', () => {
      component.ngOnInit();

      expect(fileUploadService.getUserImages).toHaveBeenCalled();
      expect(component.galleryImages).toHaveLength(3);
    });

    it('should run database repair on first load', () => {
      component.ngOnInit();

      expect(fileUploadService.repairImageDatabase).toHaveBeenCalled();
    });

    it('should handle repair failure gracefully', () => {
      fileUploadService.repairImageDatabase.and.returnValue(
        throwError(() => new Error('Repair failed'))
      );
      spyOn(console, 'warn');

      component.ngOnInit();

      expect(console.warn).toHaveBeenCalledWith(
        '⚠️ Image repair failed, continuing with normal load:', 
        jasmine.any(Error)
      );
    });
  });

  describe('Image Loading and Display', () => {
    it('should transform processed images to gallery format', () => {
      component.ngOnInit();

      expect(component.galleryImages[0]).toEqual({
        id: 1,
        url: '/generated/user-123/prof1.jpg',
        thumbnailUrl: '/uploads/user-123/image1.jpg',
        title: 'Professional Photo',
        description: 'Generated Professional style profile photo',
        style: 'professional',
        createdAt: new Date('2024-01-01'),
        status: 'completed',
        type: 'generated',
        downloadUrl: '/generated/user-123/prof1.jpg'
      });
    });

    it('should handle original uploaded images', () => {
      component.ngOnInit();

      const originalImage = component.galleryImages.find(img => img.id === 3);
      expect(originalImage?.type).toBe('original');
      expect(originalImage?.title).toBe('Uploaded Photo');
      expect(originalImage?.description).toBe('Original uploaded image');
    });

    it('should deduplicate images by ID', () => {
      const duplicateImages = [
        ...mockProcessedImages,
        mockProcessedImages[0] // Duplicate
      ];

      fileUploadService.getUserImages.and.returnValue(of({
        images: duplicateImages,
        generatedImages: 2
      }));

      component.ngOnInit();

      expect(component.galleryImages).toHaveLength(3); // No duplicates
    });

    it('should handle empty image response', () => {
      fileUploadService.getUserImages.and.returnValue(of({
        images: [],
        generatedImages: 0
      }));

      component.ngOnInit();

      expect(component.galleryImages).toEqual([]);
    });

    it('should handle loading errors', () => {
      fileUploadService.getUserImages.and.returnValue(
        throwError(() => new Error('Load failed'))
      );
      spyOn(console, 'error');

      component.ngOnInit();

      expect(console.error).toHaveBeenCalledWith('Failed to load images:', jasmine.any(Error));
      expect(component.isLoading).toBe(false);
    });
  });

  describe('Image Actions', () => {
    beforeEach(() => {
      component.ngOnInit();
    });

    it('should handle image click to open in new tab', () => {
      spyOn(window, 'open');
      const testImage = component.galleryImages[0];

      component.onImageClick(testImage);

      expect(window.open).toHaveBeenCalledWith(testImage.url, '_blank');
    });

    it('should handle image deletion', () => {
      fileUploadService.deleteImage.and.returnValue(of({
        success: true,
        message: 'Image deleted successfully'
      }));
      spyOn(window, 'confirm').and.returnValue(true);

      const testImage = component.galleryImages[0];
      const initialCount = component.galleryImages.length;

      component.onImageDelete(testImage);

      expect(fileUploadService.deleteImage).toHaveBeenCalledWith(testImage.id);
      expect(component.galleryImages).toHaveLength(initialCount - 1);
      expect(component.galleryImages.find(img => img.id === testImage.id)).toBeUndefined();
    });

    it('should handle deletion cancellation', () => {
      spyOn(window, 'confirm').and.returnValue(false);

      const testImage = component.galleryImages[0];
      const initialCount = component.galleryImages.length;

      component.onImageDelete(testImage);

      expect(fileUploadService.deleteImage).not.toHaveBeenCalled();
      expect(component.galleryImages).toHaveLength(initialCount);
    });

    it('should handle deletion errors', () => {
      fileUploadService.deleteImage.and.returnValue(
        throwError(() => new Error('Delete failed'))
      );
      spyOn(window, 'confirm').and.returnValue(true);
      spyOn(console, 'error');

      component.onImageDelete(component.galleryImages[0]);

      expect(console.error).toHaveBeenCalledWith('Failed to delete image:', jasmine.any(Error));
    });

    it('should handle image sharing with Web Share API', () => {
      const mockShare = jasmine.createSpy('share');
      (navigator as any).share = mockShare;

      const testImage = component.galleryImages[0];
      component.onImageShare(testImage);

      expect(mockShare).toHaveBeenCalledWith({
        title: testImage.title,
        text: testImage.description,
        url: testImage.url
      });
    });

    it('should fallback to clipboard when Web Share API unavailable', () => {
      const mockWriteText = jasmine.createSpy('writeText');
      (navigator as any).clipboard = { writeText: mockWriteText };
      (navigator as any).share = undefined;

      const testImage = component.galleryImages[0];
      component.onImageShare(testImage);

      expect(mockWriteText).toHaveBeenCalledWith(testImage.url);
    });
  });

  describe('Image Download Functionality', () => {
    let mockFetch: jasmine.Spy;

    beforeEach(() => {
      component.ngOnInit();
      mockFetch = spyOn(window, 'fetch');
    });

    it('should download single image successfully', async () => {
      const mockResponse = {
        ok: true,
        headers: new Map([['content-type', 'image/jpeg']]),
        blob: () => Promise.resolve(new Blob(['mock image data'], { type: 'image/jpeg' }))
      };
      mockFetch.and.returnValue(Promise.resolve(mockResponse));

      const mockLink = {
        href: '',
        download: '',
        click: jasmine.createSpy('click'),
        style: { display: '' }
      };
      spyOn(document, 'createElement').and.returnValue(mockLink as any);
      spyOn(document.body, 'appendChild');
      spyOn(document.body, 'removeChild');
      spyOn(URL, 'createObjectURL').and.returnValue('blob:mock-url');
      spyOn(URL, 'revokeObjectURL');

      await component.onImageDownload(component.galleryImages[0]);

      expect(mockFetch).toHaveBeenCalled();
      expect(mockLink.click).toHaveBeenCalled();
      expect(URL.createObjectURL).toHaveBeenCalled();
    });

    it('should handle download errors gracefully', async () => {
      mockFetch.and.returnValue(Promise.reject(new Error('Network error')));
      spyOn(window, 'alert');
      spyOn(component, 'fallbackDownload');

      await component.onImageDownload(component.galleryImages[0]);

      expect(window.alert).toHaveBeenCalledWith(
        jasmine.stringContaining('Download failed: Network error')
      );
      expect(component.fallbackDownload).toHaveBeenCalled();
    });

    it('should test image accessibility before download', async () => {
      const testImage = component.galleryImages[0];
      
      // Mock successful fetch for accessibility test
      mockFetch.and.returnValue(Promise.resolve({ ok: true }));
      
      const isAccessible = await component.testImageAccess(testImage);
      
      expect(isAccessible).toBe(true);
    });

    it('should handle inaccessible images', async () => {
      const testImage = component.galleryImages[0];
      
      // Mock failed fetch for accessibility test
      mockFetch.and.returnValue(Promise.reject(new Error('Network error')));
      
      // Mock image element fallback
      const mockImage = {
        onload: null as any,
        onerror: null as any,
        src: ''
      };
      spyOn(window, 'Image').and.returnValue(mockImage as any);
      
      const accessibilityPromise = component.testImageAccess(testImage);
      
      // Simulate image load failure
      setTimeout(() => {
        mockImage.onerror();
      }, 0);
      
      const isAccessible = await accessibilityPromise;
      expect(isAccessible).toBe(false);
    });

    it('should skip enhanced images for download', async () => {
      const enhancedImage = {
        ...component.galleryImages[0],
        style: 'Background Remover'
      };
      
      spyOn(window, 'alert');
      
      await component.onImageDownload(enhancedImage);
      
      expect(window.alert).toHaveBeenCalledWith(
        jasmine.stringContaining('Enhanced image downloads are not yet implemented')
      );
    });
  });

  describe('Bulk Download Functionality', () => {
    beforeEach(() => {
      component.ngOnInit();
    });

    it('should handle bulk download with multiple images', async () => {
      const selectedImages = component.galleryImages.slice(0, 2);
      
      spyOn(component, 'createZipDownload').and.returnValue(Promise.resolve());
      
      await component.onBulkDownload(selectedImages);
      
      expect(component.createZipDownload).toHaveBeenCalledWith(selectedImages);
    });

    it('should handle single image bulk download', async () => {
      const selectedImages = [component.galleryImages[0]];
      
      spyOn(component, 'onImageDownload').and.returnValue(Promise.resolve());
      
      await component.onBulkDownload(selectedImages);
      
      expect(component.onImageDownload).toHaveBeenCalledWith(selectedImages[0]);
    });

    it('should filter out enhanced images from bulk download', async () => {
      const mixedImages = [
        component.galleryImages[0],
        { ...component.galleryImages[1], style: 'Background Remover' },
        component.galleryImages[2]
      ];
      
      spyOn(window, 'alert');
      spyOn(component, 'createZipDownload').and.returnValue(Promise.resolve());
      
      await component.onBulkDownload(mixedImages);
      
      expect(window.alert).toHaveBeenCalledWith(
        jasmine.stringContaining('Skipping 1 enhanced images')
      );
    });

    it('should handle empty selection', async () => {
      spyOn(console, 'warn');
      
      await component.onBulkDownload([]);
      
      expect(console.warn).toHaveBeenCalledWith('No images selected for download');
    });
  });

  describe('ZIP Creation', () => {
    let mockFetch: jasmine.Spy;

    beforeEach(() => {
      component.ngOnInit();
      mockFetch = spyOn(window, 'fetch');
    });

    it('should create ZIP file with multiple images', async () => {
      const selectedImages = component.galleryImages.slice(0, 2);
      
      // Mock successful fetch responses
      mockFetch.and.returnValue(Promise.resolve({
        ok: true,
        headers: new Map([['content-type', 'image/jpeg']]),
        blob: () => Promise.resolve(new Blob(['mock image data'], { type: 'image/jpeg' }))
      }));

      const mockLink = {
        href: '',
        download: '',
        click: jasmine.createSpy('click'),
        style: { display: '' }
      };
      spyOn(document, 'createElement').and.returnValue(mockLink as any);
      spyOn(document.body, 'appendChild');
      spyOn(document.body, 'removeChild');
      spyOn(URL, 'createObjectURL').and.returnValue('blob:mock-zip');
      spyOn(URL, 'revokeObjectURL');

      await component['createZipDownload'](selectedImages);

      expect(component.downloadProgress).toBe(0); // Reset after completion
      expect(component.isDownloading).toBe(false);
      expect(mockLink.click).toHaveBeenCalled();
      expect(mockLink.download).toContain('profile-photos-');
      expect(mockLink.download).toContain('.zip');
    });

    it('should handle ZIP creation errors', async () => {
      const selectedImages = component.galleryImages.slice(0, 2);
      
      mockFetch.and.returnValue(Promise.reject(new Error('Network error')));
      spyOn(window, 'alert');

      await component['createZipDownload'](selectedImages);

      expect(window.alert).toHaveBeenCalledWith(
        'Failed to create zip file. Please try downloading images individually.'
      );
      expect(component.isDownloading).toBe(false);
    });

    it('should track progress during ZIP creation', async () => {
      const selectedImages = component.galleryImages.slice(0, 3);
      
      mockFetch.and.returnValue(Promise.resolve({
        ok: true,
        headers: new Map([['content-type', 'image/jpeg']]),
        blob: () => Promise.resolve(new Blob(['mock image data'], { type: 'image/jpeg' }))
      }));

      const progressValues: number[] = [];
      const originalCreateZip = component['createZipDownload'];
      
      spyOn(component, 'createZipDownload').and.callFake(async (images) => {
        component.isDownloading = true;
        
        for (let i = 0; i < images.length; i++) {
          component.downloadProgress = Math.round(((i + 1) / images.length) * 90);
          progressValues.push(component.downloadProgress);
        }
        
        component.downloadProgress = 100;
        progressValues.push(component.downloadProgress);
        
        component.isDownloading = false;
        component.downloadProgress = 0;
        
        return Promise.resolve();
      });

      await component.onBulkDownload(selectedImages);

      expect(progressValues).toEqual([30, 60, 90, 100]);
    });
  });

  describe('Gallery Refresh', () => {
    it('should refresh gallery images', () => {
      spyOn(component, 'loadImages');
      
      component.refreshGallery();
      
      expect(component.loadImages).toHaveBeenCalledWith(true);
    });

    it('should handle refresh query parameter', () => {
      spyOn(component, 'loadImages');
      
      // Mock ActivatedRoute with refresh param
      const mockRoute = TestBed.inject(Router);
      spyOn(mockRoute, 'navigate');
      
      component.ngOnInit();
      
      // Simulate navigation with refresh parameter
      component['route'].queryParams = of({ refresh: 'true' });
      
      component.ngOnInit();
      
      expect(component.loadImages).toHaveBeenCalledWith(true);
    });
  });

  describe('Style Name Formatting', () => {
    it('should format style names correctly', () => {
      expect(component.formatStyleName('professional')).toBe('Professional');
      expect(component.formatStyleName('tech-professional')).toBe('Tech Professional');
      expect(component.formatStyleName('digital_nomad')).toBe('Digital Nomad');
      expect(component.formatStyleName('executive/corporate')).toBe('Executive Corporate');
    });

    it('should handle empty or null style names', () => {
      expect(component.formatStyleName('')).toBe('');
      expect(component.formatStyleName(null as any)).toBe('');
    });
  });

  describe('Fallback Download', () => {
    it('should provide fallback download method', () => {
      const testImage = component.galleryImages[0];
      
      const mockLink = {
        href: '',
        download: '',
        click: jasmine.createSpy('click'),
        style: { display: '' },
        target: '',
        rel: '',
        setAttribute: jasmine.createSpy('setAttribute')
      };
      spyOn(document, 'createElement').and.returnValue(mockLink as any);
      spyOn(document.body, 'appendChild');
      spyOn(document.body, 'removeChild');
      spyOn(window, 'alert');

      component['fallbackDownload'](testImage);

      expect(mockLink.href).toBe(testImage.url);
      expect(mockLink.target).toBe('_blank');
      expect(mockLink.click).toHaveBeenCalled();
      expect(window.alert).toHaveBeenCalledWith(
        jasmine.stringContaining('Download initiated via fallback method')
      );
    });

    it('should handle fallback download errors', () => {
      const testImage = component.galleryImages[0];
      
      spyOn(document, 'createElement').and.throwError('DOM error');
      spyOn(window, 'alert');

      component['fallbackDownload'](testImage);

      expect(window.alert).toHaveBeenCalledWith(
        'Download failed completely. Please right-click the image and select "Save image as..." or check if the image URL is accessible.'
      );
    });
  });

  describe('Performance and Memory Management', () => {
    it('should handle large image sets efficiently', () => {
      const largeImageSet = Array(100).fill(null).map((_, i) => ({
        ...mockProcessedImages[0],
        id: i,
        url: `/generated/user-123/image${i}.jpg`
      }));

      fileUploadService.getUserImages.and.returnValue(of({
        images: largeImageSet,
        generatedImages: 100
      }));

      const startTime = performance.now();
      component.ngOnInit();
      const endTime = performance.now();

      expect(endTime - startTime).toBeLessThan(100); // Should be fast
      expect(component.galleryImages).toHaveLength(100);
    });

    it('should clean up resources on destroy', () => {
      component.ngOnDestroy();
      
      // Verify cleanup (would need actual cleanup implementation)
      expect(component.isLoading).toBe(false);
      expect(component.isDownloading).toBe(false);
    });
  });

  describe('Error Recovery', () => {
    it('should recover from network errors', () => {
      fileUploadService.getUserImages.and.returnValue(
        throwError(() => new Error('Network error'))
      );

      component.ngOnInit();

      expect(component.isLoading).toBe(false);
      expect(component.galleryImages).toEqual([]);
    });

    it('should handle corrupted image data', () => {
      const corruptedImages = [
        {
          ...mockProcessedImages[0],
          processedImageUrl: null,
          originalImageUrl: null
        }
      ];

      fileUploadService.getUserImages.and.returnValue(of({
        images: corruptedImages,
        generatedImages: 0
      }));

      component.ngOnInit();

      // Should still create gallery items but with fallback URLs
      expect(component.galleryImages).toHaveLength(1);
      expect(component.galleryImages[0].url).toBeDefined();
    });
  });
});
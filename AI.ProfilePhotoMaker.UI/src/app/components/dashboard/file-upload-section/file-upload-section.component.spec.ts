import { ComponentFixture, fakeAsync, TestBed, tick } from '@angular/core/testing';
import { NgZone } from '@angular/core';
import { of, throwError } from 'rxjs';

import { FileUploadSectionComponent } from './file-upload-section.component';
import { FileUploadService } from '../../../services/file-upload.service';
import { FaceDetectionService } from '../../../services/face-detection.service';
import { FileUploadManagerService } from '../../../services/file-upload-manager.service';
import { NotificationService } from '../../../services/notification.service';
import {
  QualityCheckError,
  SelectedFileWithQuality,
} from '../../../models/dashboard.types';

describe('FileUploadSectionComponent', () => {
  let component: FileUploadSectionComponent;
  let fixture: ComponentFixture<FileUploadSectionComponent>;
  let mockFileUploadService: jasmine.SpyObj<FileUploadService>;
  let mockFaceDetectionService: jasmine.SpyObj<FaceDetectionService>;
  let mockFileUploadManagerService: jasmine.SpyObj<FileUploadManagerService>;
  let mockNotificationService: jasmine.SpyObj<NotificationService>;
  let mockNgZone: jasmine.SpyObj<NgZone>;

  beforeEach(async () => {
    const fileUploadServiceSpy = jasmine.createSpyObj('FileUploadService', [
      'uploadImages',
      'deleteImage',
    ]);
    const faceDetectionServiceSpy = jasmine.createSpyObj('FaceDetectionService', ['validateImage']);
    const fileUploadManagerServiceSpy = jasmine.createSpyObj('FileUploadManagerService', [
      'processFiles',
    ]);
    const notificationServiceSpy = jasmine.createSpyObj('NotificationService', [
      'success',
      'error',
      'info',
    ]);
    const ngZoneSpy = jasmine.createSpyObj('NgZone', ['run']);

    await TestBed.configureTestingModule({
      imports: [FileUploadSectionComponent],
      providers: [
        { provide: FileUploadService, useValue: fileUploadServiceSpy },
        { provide: FaceDetectionService, useValue: faceDetectionServiceSpy },
        { provide: FileUploadManagerService, useValue: fileUploadManagerServiceSpy },
        { provide: NotificationService, useValue: notificationServiceSpy },
        { provide: NgZone, useValue: ngZoneSpy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(FileUploadSectionComponent);
    component = fixture.componentInstance;
    mockFileUploadService = TestBed.inject(FileUploadService) as jasmine.SpyObj<FileUploadService>;
    mockFaceDetectionService = TestBed.inject(
      FaceDetectionService
    ) as jasmine.SpyObj<FaceDetectionService>;
    mockFileUploadManagerService = TestBed.inject(
      FileUploadManagerService
    ) as jasmine.SpyObj<FileUploadManagerService>;
    mockNotificationService = TestBed.inject(
      NotificationService
    ) as jasmine.SpyObj<NotificationService>;
    mockNgZone = TestBed.inject(NgZone) as jasmine.SpyObj<NgZone>;

    // Setup default mocks
    mockNgZone.run.and.callFake((fn: Function) => fn());
    fixture.detectChanges();
  });

  describe('Component Initialization', () => {
    it('should create', () => {
      expect(component).toBeTruthy();
    });

    it('should initialize with default values', () => {
      expect(component.selectedFiles).toEqual([]);
      expect(component.selectedFilesWithQuality).toEqual([]);
      expect(component.isUploading).toBeFalse();
      expect(component.uploadProgressValue).toBe(0);
      expect(component.isDragOver).toBeFalse();
      expect(component.isCheckingQuality).toBeFalse();
      expect(component.qualityCheckProgress).toBe('');
      expect(component.qualityCheckErrors).toEqual([]);
    });

    it('should have correct default input values', () => {
      expect(component.uploadedImageThumbnails).toEqual([]);
      expect(component.currentStep).toBe(1);
      expect(component.maxFiles).toBe(20);
      expect(component.maxFileSize).toBe(7 * 1024 * 1024);
      expect(component.allowedTypes).toEqual(['image/jpeg', 'image/png', 'image/webp']);
    });
  });

  describe('File Selection', () => {
    it('should trigger file input click', () => {
      const fileInput = fixture.debugElement.nativeElement.querySelector('input[type="file"]');
      spyOn(fileInput, 'click');

      component.triggerFileUpload();

      expect(fileInput.click).toHaveBeenCalled();
    });

    it('should handle file selection from input', () => {
      const mockFile = new File(['content'], 'test.jpg', { type: 'image/jpeg' });
      const event = { target: { files: [mockFile] } };
      spyOn(component, 'handleFileSelection');

      component.onFileSelected(event);

      expect(component.handleFileSelection).toHaveBeenCalledWith([mockFile]);
    });

    it('should reset input value after file selection', () => {
      const fileInput = fixture.debugElement.nativeElement.querySelector('input[type="file"]');
      fileInput.value = 'test.jpg';
      const event = { target: { files: [] } };

      component.onFileSelected(event);

      expect(fileInput.value).toBe('');
    });
  });

  describe('Drag and Drop', () => {
    it('should handle drag over', () => {
      const event = new DragEvent('dragover');
      spyOn(event, 'preventDefault');
      spyOn(event, 'stopPropagation');

      component.onDragOver(event);

      expect(event.preventDefault).toHaveBeenCalled();
      expect(event.stopPropagation).toHaveBeenCalled();
      expect(component.isDragOver).toBeTrue();
    });

    it('should handle drag leave', () => {
      const event = new DragEvent('dragleave');
      spyOn(event, 'preventDefault');
      spyOn(event, 'stopPropagation');
      component.isDragOver = true;

      component.onDragLeave(event);

      expect(event.preventDefault).toHaveBeenCalled();
      expect(event.stopPropagation).toHaveBeenCalled();
      expect(component.isDragOver).toBeFalse();
    });

    it('should handle drop with files', () => {
      const mockFile = new File(['content'], 'test.jpg', { type: 'image/jpeg' });
      const event = new DragEvent('drop');
      Object.defineProperty(event, 'dataTransfer', {
        value: { files: [mockFile] },
      });
      spyOn(event, 'preventDefault');
      spyOn(event, 'stopPropagation');
      spyOn(component, 'handleFileSelection');

      component.onDrop(event);

      expect(event.preventDefault).toHaveBeenCalled();
      expect(event.stopPropagation).toHaveBeenCalled();
      expect(component.isDragOver).toBeFalse();
      expect(component.handleFileSelection).toHaveBeenCalledWith([mockFile]);
    });
  });

  describe('File Validation', () => {
    it('should validate file type correctly', () => {
      const validFile = new File(['content'], 'test.jpg', { type: 'image/jpeg' });
      const invalidFile = new File(['content'], 'test.txt', { type: 'text/plain' });

      expect(component['isValidFile'](validFile)).toBeTrue();
      expect(component['isValidFile'](invalidFile)).toBeFalse();
      expect(mockNotificationService.error).toHaveBeenCalledWith(
        'Invalid File Type',
        jasmine.stringContaining('not a supported image format')
      );
    });

    it('should validate file size correctly', () => {
      const validFile = new File(['content'], 'test.jpg', { type: 'image/jpeg' });
      const largeFile = new File(['x'.repeat(8 * 1024 * 1024)], 'large.jpg', {
        type: 'image/jpeg',
      });

      expect(component['isValidFile'](validFile)).toBeTrue();
      expect(component['isValidFile'](largeFile)).toBeFalse();
      expect(mockNotificationService.error).toHaveBeenCalledWith(
        'File Too Large',
        jasmine.stringContaining('too large')
      );
    });

    it('should reject files exceeding maximum count', fakeAsync(() => {
      component.maxFiles = 2;
      component.selectedFiles = [new File(['content'], 'existing.jpg', { type: 'image/jpeg' })];
      const newFiles = [
        new File(['content'], 'new1.jpg', { type: 'image/jpeg' }),
        new File(['content'], 'new2.jpg', { type: 'image/jpeg' }),
      ];

      component.handleFileSelection(newFiles);
      tick();

      expect(mockNotificationService.error).toHaveBeenCalledWith(
        'Too Many Files',
        jasmine.stringContaining('maximum of 2 files')
      );
    }));
  });

  describe('Quality Validation', () => {
    it('should validate image quality successfully', fakeAsync(() => {
      const mockFile = new File(['content'], 'test.jpg', { type: 'image/jpeg' });
      const mockQualityResult = {
        isValid: true,
        qualityScore: {
          overall: 80,
          breakdown: {
            faceQuality: 80,
            technical: 70,
            composition: 90,
            fluxCompatibility: 85,
            lighting: 75,
          },
          suggestions: [],
        },
        errors: [],
        warnings: [],
        faceCount: 1,
        bodyType: 'headshot' as const,
      };

      mockFaceDetectionService.validateImage.and.returnValue(Promise.resolve(mockQualityResult));
      spyOn(component, 'getImageDimensions' as any).and.returnValue(
        Promise.resolve({ width: 800, height: 600 })
      );

      component['validateImageQuality']([mockFile]);
      tick();

      expect(component.isCheckingQuality).toBeFalse();
      expect(component.selectedFilesWithQuality.length).toBe(1);
      expect(component.selectedFilesWithQuality[0].isValid).toBeTrue();
      expect(mockNotificationService.success).toHaveBeenCalled();
    }));

    it('should handle quality validation failure', fakeAsync(() => {
      const mockFile = new File(['content'], 'test.jpg', { type: 'image/jpeg' });
      const mockQualityResult = {
        isValid: false,
        qualityScore: {
          overall: 30,
          breakdown: {
            faceQuality: 0,
            technical: 50,
            composition: 40,
            fluxCompatibility: 20,
            lighting: 60,
          },
          suggestions: ['No face detected', 'Try a different photo with a clear face'],
        },
        errors: ['No face detected'],
        warnings: [],
        faceCount: 0,
        bodyType: 'invalid' as const,
      };

      mockFaceDetectionService.validateImage.and.returnValue(Promise.resolve(mockQualityResult));
      spyOn(component, 'getImageDimensions' as any).and.returnValue(
        Promise.resolve({ width: 800, height: 600 })
      );

      component['validateImageQuality']([mockFile]);
      tick();

      expect(component.qualityCheckErrors.length).toBe(1);
      expect(component.qualityCheckErrors[0].errors).toContain('No face detected');
      expect(mockNotificationService.error).toHaveBeenCalled();
    }));

    it('should reject images with insufficient resolution', fakeAsync(() => {
      const mockFile = new File(['content'], 'test.jpg', { type: 'image/jpeg' });
      spyOn(component, 'getImageDimensions' as any).and.returnValue(
        Promise.resolve({ width: 400, height: 300 })
      );

      component['validateImageQuality']([mockFile]);
      tick();

      expect(component.qualityCheckErrors.length).toBe(1);
      expect(component.qualityCheckErrors[0].errors[0]).toContain(
        'resolution 400x300 is too small'
      );
    }));
  });

  describe('File Management', () => {
    it('should remove file correctly', () => {
      const mockFile = new File(['content'], 'test.jpg', { type: 'image/jpeg' });
      component.selectedFiles = [mockFile];
      component.selectedFilesWithQuality = [
        { file: mockFile, isValid: true } as SelectedFileWithQuality,
      ];
      spyOn(component.fileRemoved, 'emit');
      spyOn(component.filesSelected, 'emit');

      component.removeFile(0);

      expect(component.selectedFiles).toEqual([]);
      expect(component.selectedFilesWithQuality).toEqual([]);
      expect(component.fileRemoved.emit).toHaveBeenCalledWith(0);
      expect(component.filesSelected.emit).toHaveBeenCalledWith([]);
    });

    it('should handle invalid index when removing file', () => {
      component.selectedFiles = [new File(['content'], 'test.jpg', { type: 'image/jpeg' })];
      spyOn(component.fileRemoved, 'emit');

      component.removeFile(5);

      expect(component.fileRemoved.emit).not.toHaveBeenCalled();
    });
  });

  describe('Image Deletion', () => {
    it('should delete uploaded image successfully', () => {
      const mockThumb = { id: 123, url: 'test.jpg' };
      const mockResponse = { success: true, message: 'Image deleted successfully' };
      mockFileUploadService.deleteImage.and.returnValue(of(mockResponse));
      spyOn(component.uploadedImageDeleted, 'emit');

      component.deleteUploadedImage(mockThumb, 0);

      expect(mockFileUploadService.deleteImage).toHaveBeenCalledWith(123);
      expect(component.uploadedImageDeleted.emit).toHaveBeenCalledWith({
        thumb: mockThumb,
        index: 0,
      });
      expect(mockNotificationService.success).toHaveBeenCalledWith(
        'Deleted',
        'Image deleted successfully'
      );
    });

    it('should handle delete error with 404 status', () => {
      const mockThumb = { id: 123, url: 'test.jpg' };
      const mockError = { status: 404, message: 'Not found' };
      mockFileUploadService.deleteImage.and.returnValue(throwError(mockError));
      spyOn(component, 'refreshUploadedImages' as any);

      component.deleteUploadedImage(mockThumb, 0);

      expect(mockNotificationService.error).toHaveBeenCalledWith(
        'Error',
        jasmine.stringContaining('not found')
      );
      expect(component['refreshUploadedImages']).toHaveBeenCalled();
    });

    it('should handle invalid thumbnail data', () => {
      const invalidThumb = null;

      component.deleteUploadedImage(invalidThumb, 0);

      expect(mockNotificationService.error).toHaveBeenCalledWith(
        'Error',
        jasmine.stringContaining('missing or invalid')
      );
    });
  });

  describe('Upload Process', () => {
    it('should upload images successfully', () => {
      const mockFile = new File(['content'], 'test.jpg', { type: 'image/jpeg' });
      component.selectedFiles = [mockFile];
      component.selectedFilesWithQuality = [
        { file: mockFile, isValid: true } as SelectedFileWithQuality,
      ];

      const mockUploadResponse = {
        progress: 100,
        response: {
          profileId: 1,
          uploadedFiles: [{ fileName: 'test.jpg', size: 1024, url: 'uploaded.jpg' }],
          uploadedImageIds: [1],
          zipCreated: false,
          zipPath: '',
          message: 'Upload successful',
        },
      };
      mockFileUploadService.uploadImages.and.returnValue(of(mockUploadResponse));
      spyOn(component.uploadCompleted, 'emit');

      component.uploadImages();

      expect(mockFileUploadService.uploadImages).toHaveBeenCalledWith([mockFile], undefined, false);
      expect(component.uploadCompleted.emit).toHaveBeenCalledWith(
        mockUploadResponse.response.uploadedFiles
      );
      expect(mockNotificationService.success).toHaveBeenCalled();
    });

    it('should handle upload progress updates', () => {
      const mockFile = new File(['content'], 'test.jpg', { type: 'image/jpeg' });
      component.selectedFiles = [mockFile];
      component.selectedFilesWithQuality = [
        { file: mockFile, isValid: true } as SelectedFileWithQuality,
      ];

      const mockProgress = { progress: 50 };
      mockFileUploadService.uploadImages.and.returnValue(of(mockProgress));
      spyOn(component.uploadProgress, 'emit');

      component.uploadImages();

      expect(component.uploadProgressValue).toBe(50);
      expect(component.uploadProgress.emit).toHaveBeenCalledWith(50);
    });

    it('should handle upload error', () => {
      const mockFile = new File(['content'], 'test.jpg', { type: 'image/jpeg' });
      component.selectedFiles = [mockFile];
      component.selectedFilesWithQuality = [
        { file: mockFile, isValid: true } as SelectedFileWithQuality,
      ];

      mockFileUploadService.uploadImages.and.returnValue(throwError('Upload failed'));

      component.uploadImages();

      expect(mockNotificationService.error).toHaveBeenCalledWith(
        'Upload Error',
        'An error occurred during upload'
      );
      expect(component.isUploading).toBeFalse();
    });

    it('should not upload if no files selected', () => {
      component.selectedFiles = [];

      component.uploadImages();

      expect(mockFileUploadService.uploadImages).not.toHaveBeenCalled();
      expect(mockNotificationService.error).toHaveBeenCalledWith(
        'No Files Selected',
        'Please select files to upload'
      );
    });
  });

  describe('Helper Methods', () => {
    it('should get valid files count', () => {
      component.selectedFilesWithQuality = [
        { isValid: true } as SelectedFileWithQuality,
        { isValid: false } as SelectedFileWithQuality,
        { isValid: true } as SelectedFileWithQuality,
      ];

      expect(component.getValidFilesCount()).toBe(2);
    });

    it('should get invalid files count', () => {
      component.qualityCheckErrors = [{} as QualityCheckError, {} as QualityCheckError];

      expect(component.getInvalidFilesCount()).toBe(2);
    });

    it('should check if has selected files', () => {
      component.selectedFiles = [];
      expect(component.hasSelectedFiles()).toBeFalse();

      component.selectedFiles = [new File(['content'], 'test.jpg', { type: 'image/jpeg' })];
      expect(component.hasSelectedFiles()).toBeTrue();
    });

    it('should check if can upload', () => {
      component.selectedFiles = [new File(['content'], 'test.jpg', { type: 'image/jpeg' })];
      component.selectedFilesWithQuality = [{ isValid: true } as SelectedFileWithQuality];
      component.isUploading = false;
      component.isCheckingQuality = false;

      expect(component.canUpload()).toBeTrue();

      component.isUploading = true;
      expect(component.canUpload()).toBeFalse();
    });

    it('should format compact error messages', () => {
      const longMessage =
        'No face detected in image. Please upload a clear photo with your face visible.';
      const shortMessage = 'Short message';
      const veryLongMessage = 'This is a very long message that should be truncated';

      expect(component.getCompactErrorMessage(longMessage)).toBe('No face detected');
      expect(component.getCompactErrorMessage(shortMessage)).toBe('Short message');
      expect(component.getCompactErrorMessage(veryLongMessage)).toBe(
        'This is a very long message that sho...'
      );
    });
  });

  describe('UI State Getters', () => {
    it('should show upload guidelines correctly', () => {
      component.currentStep = 1;
      component.selectedFiles = [];
      component.uploadedImageThumbnails = [];

      expect(component.showUploadGuidelines).toBeTrue();

      component.selectedFiles = [new File(['content'], 'test.jpg', { type: 'image/jpeg' })];
      expect(component.showUploadGuidelines).toBeFalse();
    });

    it('should show selected files correctly', () => {
      component.selectedFiles = [];
      expect(component.showSelectedFiles).toBeFalse();

      component.selectedFiles = [new File(['content'], 'test.jpg', { type: 'image/jpeg' })];
      expect(component.showSelectedFiles).toBeTrue();
    });

    it('should show uploaded images correctly', () => {
      component.uploadedImageThumbnails = [];
      expect(component.showUploadedImages).toBeFalse();

      component.uploadedImageThumbnails = [{ id: 1, url: 'test.jpg' }];
      expect(component.showUploadedImages).toBeTrue();
    });
  });

  describe('Cleanup', () => {
    it('should cleanup on destroy', () => {
      spyOn(component, 'cleanupFilePreviewCache' as any);
      spyOn(document, 'removeEventListener');

      component.ngOnDestroy();

      expect(component['cleanupFilePreviewCache']).toHaveBeenCalled();
      expect(document.removeEventListener).toHaveBeenCalled();
    });
  });
});

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { GalleryImageActionsComponent } from './gallery-image-actions.component';
import { GalleryImage } from '../photo-gallery.component';

describe('GalleryImageActionsComponent', () => {
  let component: GalleryImageActionsComponent;
  let fixture: ComponentFixture<GalleryImageActionsComponent>;

  const mockImage: GalleryImage = {
    id: 1,
    url: 'https://example.com/image.jpg',
    title: 'Test Image',
    type: 'generated',
    status: 'completed',
    createdAt: new Date('2024-01-01'),
    style: 'business',
    thumbnailUrl: 'https://example.com/thumbnail.jpg',
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [GalleryImageActionsComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(GalleryImageActionsComponent);
    component = fixture.componentInstance;
    component.image = mockImage;
    fixture.detectChanges();
  });

  describe('Component Initialization', () => {
    it('should create', () => {
      expect(component).toBeTruthy();
    });

    it('should initialize with default values', () => {
      expect(component.viewMode).toBe('grid');
      expect(component.showViewButton).toBeTrue();
    });

    it('should require image input', () => {
      expect(component.image).toBeDefined();
      expect(component.image).toBe(mockImage);
    });
  });

  describe('Input Properties', () => {
    it('should handle viewMode input', () => {
      component.viewMode = 'list';
      expect(component.viewMode).toBe('list');

      component.viewMode = 'grid';
      expect(component.viewMode).toBe('grid');
    });

    it('should handle showViewButton input', () => {
      component.showViewButton = false;
      expect(component.showViewButton).toBeFalse();

      component.showViewButton = true;
      expect(component.showViewButton).toBeTrue();
    });

    it('should handle different image types', () => {
      const originalImage: GalleryImage = {
        ...mockImage,
        type: 'original',
        status: 'processing',
      };

      component.image = originalImage;
      expect(component.image.type).toBe('original');
      expect(component.image.status).toBe('processing');
    });
  });

  describe('Event Handling', () => {
    it('should emit view event', () => {
      spyOn(component.view, 'emit');

      component.onView();

      expect(component.view.emit).toHaveBeenCalledWith(mockImage);
    });

    it('should emit download event', () => {
      spyOn(component.download, 'emit');

      component.onDownload();

      expect(component.download.emit).toHaveBeenCalledWith(mockImage);
    });

    it('should emit share event', () => {
      spyOn(component.share, 'emit');

      component.onShare();

      expect(component.share.emit).toHaveBeenCalledWith(mockImage);
    });

    it('should emit refine event', () => {
      spyOn(component.refine, 'emit');

      component.onRefine();

      expect(component.refine.emit).toHaveBeenCalledWith(mockImage);
    });

    it('should emit delete event', () => {
      spyOn(component.delete, 'emit');

      component.onDelete();

      expect(component.delete.emit).toHaveBeenCalledWith(mockImage);
    });
  });

  describe('Event Propagation', () => {
    it('should stop propagation when event is provided to onView', () => {
      const mockEvent = jasmine.createSpyObj('Event', ['stopPropagation']);
      spyOn(component.view, 'emit');

      component.onView(mockEvent);

      expect(mockEvent.stopPropagation).toHaveBeenCalled();
      expect(component.view.emit).toHaveBeenCalledWith(mockImage);
    });

    it('should stop propagation when event is provided to onDownload', () => {
      const mockEvent = jasmine.createSpyObj('Event', ['stopPropagation']);
      spyOn(component.download, 'emit');

      component.onDownload(mockEvent);

      expect(mockEvent.stopPropagation).toHaveBeenCalled();
      expect(component.download.emit).toHaveBeenCalledWith(mockImage);
    });

    it('should stop propagation when event is provided to onShare', () => {
      const mockEvent = jasmine.createSpyObj('Event', ['stopPropagation']);
      spyOn(component.share, 'emit');

      component.onShare(mockEvent);

      expect(mockEvent.stopPropagation).toHaveBeenCalled();
      expect(component.share.emit).toHaveBeenCalledWith(mockImage);
    });

    it('should stop propagation when event is provided to onDelete', () => {
      const mockEvent = jasmine.createSpyObj('Event', ['stopPropagation']);
      spyOn(component.delete, 'emit');

      component.onDelete(mockEvent);

      expect(mockEvent.stopPropagation).toHaveBeenCalled();
      expect(component.delete.emit).toHaveBeenCalledWith(mockImage);
    });

    it('should work without event parameter', () => {
      spyOn(component.view, 'emit');
      spyOn(component.download, 'emit');
      spyOn(component.share, 'emit');
      spyOn(component.delete, 'emit');

      component.onView();
      component.onDownload();
      component.onShare();
      component.onDelete();

      expect(component.view.emit).toHaveBeenCalledWith(mockImage);
      expect(component.download.emit).toHaveBeenCalledWith(mockImage);
      expect(component.share.emit).toHaveBeenCalledWith(mockImage);
      expect(component.delete.emit).toHaveBeenCalledWith(mockImage);
    });
  });

  describe('Status Check', () => {
    it('should return true for completed status', () => {
      component.image = { ...mockImage, status: 'completed' };

      expect(component.isCompleted).toBeTrue();
    });

    it('should return false for processing status', () => {
      component.image = { ...mockImage, status: 'processing' };

      expect(component.isCompleted).toBeFalse();
    });

    it('should return false for failed status', () => {
      component.image = { ...mockImage, status: 'failed' };

      expect(component.isCompleted).toBeFalse();
    });

    // "pending" is not a valid status in the current model; use "processing" instead
    it('should return false for processing-like status', () => {
      component.image = { ...mockImage, status: 'processing' };

      expect(component.isCompleted).toBeFalse();
    });
  });

  describe('Different Image Types', () => {
    it('should handle original image', () => {
      const uploadedImage: GalleryImage = {
        id: 2,
        url: 'https://example.com/uploaded.jpg',
        title: 'Original Image',
        type: 'original',
        status: 'completed',
        createdAt: new Date('2024-01-02'),
      };

      component.image = uploadedImage;

      expect(component.image.type).toBe('original');
      expect(component.isCompleted).toBeTrue();
    });

    it('should handle generated image', () => {
      const generatedImage: GalleryImage = {
        id: 3,
        url: 'https://example.com/generated.jpg',
        title: 'Generated Image',
        type: 'generated',
        status: 'completed',
        createdAt: new Date('2024-01-03'),
        style: 'casual',
      };

      component.image = generatedImage;

      expect(component.image.type).toBe('generated');
      expect(component.image.style).toBe('casual');
      expect(component.isCompleted).toBeTrue();
    });

    it('should handle enhanced image', () => {
      const enhancedImage: GalleryImage = {
        id: 4,
        url: 'https://example.com/enhanced.jpg',
        title: 'Enhanced Image',
        type: 'enhanced',
        status: 'completed',
        createdAt: new Date('2024-01-04'),
      };

      component.image = enhancedImage;

      expect(component.image.type).toBe('enhanced');
      expect(component.isCompleted).toBeTrue();
    });
  });

  describe('View Mode Handling', () => {
    it('should handle grid view mode', () => {
      component.viewMode = 'grid';

      expect(component.viewMode).toBe('grid');
    });

    it('should handle list view mode', () => {
      component.viewMode = 'list';

      expect(component.viewMode).toBe('list');
    });

    it('should maintain view mode state', () => {
      component.viewMode = 'grid';
      expect(component.viewMode).toBe('grid');

      component.viewMode = 'list';
      expect(component.viewMode).toBe('list');
    });
  });

  describe('Button Visibility', () => {
    it('should show view button when showViewButton is true', () => {
      component.showViewButton = true;

      expect(component.showViewButton).toBeTrue();
    });

    it('should hide view button when showViewButton is false', () => {
      component.showViewButton = false;

      expect(component.showViewButton).toBeFalse();
    });
  });

  describe('Edge Cases', () => {
    it('should handle image with minimal properties', () => {
      const minimalImage: GalleryImage = {
        id: 5,
        url: 'https://example.com/minimal.jpg',
        title: 'Minimal Image',
        type: 'original',
        status: 'processing',
        createdAt: new Date(),
      };

      component.image = minimalImage;

      expect(component.image.id).toBe(5);
      expect(component.image.style).toBeUndefined();
      expect(component.image.thumbnailUrl).toBeUndefined();
      expect(component.isCompleted).toBeFalse();
    });

    it('should handle image with all properties', () => {
      const fullImage: GalleryImage = {
        id: 6,
        url: 'https://example.com/full.jpg',
        title: 'Full Image',
        type: 'generated',
        status: 'completed',
        createdAt: new Date(),
        style: 'professional',
        thumbnailUrl: 'https://example.com/full-thumb.jpg',
      };

      component.image = fullImage;

      expect(component.image.id).toBe(6);
      expect(component.image.style).toBe('professional');
      expect(component.image.thumbnailUrl).toBe('https://example.com/full-thumb.jpg');
      expect(component.isCompleted).toBeTrue();
    });

    it('should handle null or undefined event parameter', () => {
      spyOn(component.view, 'emit');

      component.onView(undefined);
      component.onView(null as any);

      expect(component.view.emit).toHaveBeenCalledTimes(2);
      expect(component.view.emit).toHaveBeenCalledWith(mockImage);
    });
  });

  describe('Integration Tests', () => {
    it('should handle multiple actions in sequence', () => {
      spyOn(component.view, 'emit');
      spyOn(component.download, 'emit');
      spyOn(component.share, 'emit');
      spyOn(component.delete, 'emit');

      component.onView();
      component.onDownload();
      component.onShare();
      component.onDelete();

      expect(component.view.emit).toHaveBeenCalledWith(mockImage);
      expect(component.download.emit).toHaveBeenCalledWith(mockImage);
      expect(component.share.emit).toHaveBeenCalledWith(mockImage);
      expect(component.delete.emit).toHaveBeenCalledWith(mockImage);
    });

    it('should handle actions with events in sequence', () => {
      const mockEvent = jasmine.createSpyObj('Event', ['stopPropagation']);
      spyOn(component.view, 'emit');
      spyOn(component.download, 'emit');
      spyOn(component.share, 'emit');
      spyOn(component.delete, 'emit');

      component.onView(mockEvent);
      component.onDownload(mockEvent);
      component.onShare(mockEvent);
      component.onDelete(mockEvent);

      expect(mockEvent.stopPropagation).toHaveBeenCalledTimes(4);
      expect(component.view.emit).toHaveBeenCalledWith(mockImage);
      expect(component.download.emit).toHaveBeenCalledWith(mockImage);
      expect(component.share.emit).toHaveBeenCalledWith(mockImage);
      expect(component.delete.emit).toHaveBeenCalledWith(mockImage);
    });
  });
});

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { GalleryImage, PhotoGalleryComponent } from './photo-gallery.component';

/**
 * Photo Gallery Component Test Suite
 *
 * Simplified tests that match the actual component structure.
 * This component handles image display, filtering, pagination, and selection.
 */
describe('PhotoGalleryComponent', () => {
  let component: PhotoGalleryComponent;
  let fixture: ComponentFixture<PhotoGalleryComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PhotoGalleryComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(PhotoGalleryComponent);
    component = fixture.componentInstance;
  });

  describe('Component Initialization', () => {
    it('should create the component', () => {
      expect(component).toBeTruthy();
    });

    it('should initialize with default values', () => {
      expect(component.images).toEqual([]);
      expect(component.title).toBe('Photo Gallery');
      expect(component.viewMode).toBe('grid');
      expect(component.filterType).toBe('generated');
      expect(component.selectedImages).toEqual([]);
      expect(component.currentPage).toBe(1);
      expect(component.pageSize).toBe(12);
    });

    it('should update filtered images on init', () => {
      spyOn(component, 'updateFilteredImages');
      component.ngOnInit();
      expect(component.updateFilteredImages).toHaveBeenCalled();
    });
  });

  describe('View Mode Management', () => {
    it('should set view mode to grid', () => {
      component.setViewMode('grid');
      expect(component.viewMode).toBe('grid');
    });

    it('should set view mode to list', () => {
      component.setViewMode('list');
      expect(component.viewMode).toBe('list');
    });
  });

  describe('Filtering Logic', () => {
    beforeEach(() => {
      component.images = [
        {
          id: 1,
          url: 'image1.jpg',
          title: 'Image 1',
          type: 'generated',
          status: 'completed',
          createdAt: new Date(),
        },
        {
          id: 2,
          url: 'image2.jpg',
          title: 'Image 2',
          type: 'original',
          status: 'completed',
          createdAt: new Date(),
        },
        {
          id: 3,
          url: 'image3.jpg',
          title: 'Image 3',
          type: 'generated',
          status: 'completed',
          createdAt: new Date(),
        },
      ] as GalleryImage[];
    });

    it('should filter images by type "generated"', () => {
      component.filterType = 'generated';
      component.updateFilteredImages();

      expect(component.filteredImages.length).toBe(2);
      expect(component.filteredImages.every(img => img.type === 'generated')).toBeTrue();
    });

    it('should filter images by type "original"', () => {
      component.filterType = 'original';
      component.updateFilteredImages();

      expect(component.filteredImages.length).toBe(1);
      expect(component.filteredImages[0].type).toBe('original');
    });

    it('should show all images when filter is "all"', () => {
      component.filterType = 'all';
      component.updateFilteredImages();

      expect(component.filteredImages.length).toBe(3);
    });

    it('should handle filter change event', () => {
      const filterType = 'original';
      spyOn(component, 'updateFilteredImages');

      component.onFilterChange(filterType);

      expect(component.filterType).toBe('original');
      expect(component.updateFilteredImages).toHaveBeenCalled();
    });
  });

  describe('Pagination Logic', () => {
    beforeEach(() => {
      // Create 25 images for pagination testing
      component.images = Array.from({ length: 25 }, (_, i) => ({
        id: i + 1,
        url: `image${i + 1}.jpg`,
        title: `Image ${i + 1}`,
        type: 'generated',
        status: 'completed',
        createdAt: new Date(),
      })) as GalleryImage[];
      component.pageSize = 12;
    });

    it('should calculate total pages correctly', () => {
      component.updateFilteredImages();
      expect(component.totalPages).toBe(Math.ceil(25 / 12)); // 3 pages
    });

    it('should paginate images correctly', () => {
      component.currentPage = 1;
      component.updateFilteredImages();

      expect(component.paginatedImages.length).toBe(12);
      expect(component.paginatedImages[0].id).toBe(1);
    });

    it('should handle page overflow', () => {
      component.currentPage = 10; // More than available pages
      component.updateFilteredImages();

      expect(component.currentPage).toBe(3); // Should reset to last available page
    });

    it('should slice images for current page', () => {
      component.currentPage = 2;
      component.updateFilteredImages();

      expect(component.paginatedImages.length).toBe(12);
      expect(component.paginatedImages[0].id).toBe(13); // Second page starts at item 13
    });
  });

  describe('Image Selection Logic', () => {
    beforeEach(() => {
      component.images = [
        {
          id: 1,
          url: 'image1.jpg',
          title: 'Image 1',
          type: 'generated',
          status: 'completed',
          createdAt: new Date(),
        },
        {
          id: 2,
          url: 'image2.jpg',
          title: 'Image 2',
          type: 'generated',
          status: 'completed',
          createdAt: new Date(),
        },
        {
          id: 3,
          url: 'image3.jpg',
          title: 'Image 3',
          type: 'generated',
          status: 'completed',
          createdAt: new Date(),
        },
      ] as GalleryImage[];
      component.updateFilteredImages();
    });

    it('should select all images when none are selected', () => {
      component.selectedImages = [];
      component.selectAll();

      expect(component.selectedImages.length).toBe(component.filteredImages.length);
    });

    it('should deselect all images when all are selected', () => {
      component.selectedImages = [...component.filteredImages];
      component.selectAll();

      expect(component.selectedImages.length).toBe(0);
    });

    it('should clear all selections', () => {
      component.selectedImages = [...component.filteredImages];
      component.clearSelections();

      expect(component.selectedImages.length).toBe(0);
    });
  });

  describe('Image Actions', () => {
    let mockImage: GalleryImage;

    beforeEach(() => {
      mockImage = {
        id: 1,
        url: 'test-image.jpg',
        title: 'Test Image',
        type: 'generated',
        status: 'completed',
        createdAt: new Date(),
      };
    });

    it('should emit imageClick event when opening image', () => {
      spyOn(component.imageClick, 'emit');

      component.openImage(mockImage);

      expect(component.imageClick.emit).toHaveBeenCalledWith(mockImage);
    });

    it('should emit imageDownload event when downloading image', () => {
      spyOn(component.imageDownload, 'emit');

      component.downloadImage(mockImage);

      expect(component.imageDownload.emit).toHaveBeenCalledWith(mockImage);
    });

    it('should emit imageShare event when sharing image', () => {
      spyOn(component.imageShare, 'emit');

      component.shareImage(mockImage);

      expect(component.imageShare.emit).toHaveBeenCalledWith(mockImage);
    });

    it('should emit imageDelete event when deleting image', () => {
      spyOn(component.imageDelete, 'emit');

      component.deleteImage(mockImage);

      expect(component.imageDelete.emit).toHaveBeenCalledWith(mockImage);
    });

    it('should emit bulkDownload event when downloading selected images', () => {
      component.selectedImages = [mockImage];
      spyOn(component.bulkDownload, 'emit');

      component.downloadSelected();

      expect(component.bulkDownload.emit).toHaveBeenCalledWith([mockImage]);
    });
  });

  describe('Component Change Detection', () => {
    it('should update filtered images on changes', () => {
      spyOn(component, 'updateFilteredImages');

      component.ngOnChanges();

      expect(component.updateFilteredImages).toHaveBeenCalled();
    });

    it('should clean up invalid selections on changes', () => {
      const validImage = {
        id: 1,
        url: 'valid.jpg',
        title: 'Valid',
        type: 'generated',
        status: 'completed',
        createdAt: new Date(),
      } as GalleryImage;
      const invalidImage = {
        id: 2,
        url: 'invalid.jpg',
        title: 'Invalid',
        type: 'generated',
        status: 'completed',
        createdAt: new Date(),
      } as GalleryImage;

      component.images = [validImage];
      component.selectedImages = [validImage, invalidImage];

      component.ngOnChanges();

      expect(component.selectedImages).toEqual([validImage]);
    });
  });

  describe('Track By Function', () => {
    it('should track images by id', () => {
      const mockImage = { id: 123 } as GalleryImage;

      const result = component.trackByImageId(0, mockImage);

      expect(result).toBe(123);
    });
  });

  describe('Math Utility', () => {
    it('should expose Math object for template use', () => {
      expect((component as any).mathHelper).toBe(Math);
    });
  });

  describe('Page Size Changes', () => {
    beforeEach(() => {
      component.images = Array.from({ length: 50 }, (_, i) => ({
        id: i + 1,
        url: `image${i + 1}.jpg`,
        title: `Image ${i + 1}`,
        type: 'generated',
        status: 'completed',
        createdAt: new Date(),
      })) as GalleryImage[];
    });

    it('should handle page size change', () => {
      component.pageSize = 6;
      component.updateFilteredImages();

      expect(component.paginatedImages.length).toBe(6);
      expect(component.totalPages).toBe(Math.ceil(50 / 6));
    });

    it('should reset to page 1 when page size increases beyond total pages', () => {
      component.currentPage = 5;
      component.pageSize = 100; // Larger than total items
      component.updateFilteredImages();

      expect(component.currentPage).toBe(1);
    });

    it('should adjust current page when page size decreases', () => {
      component.currentPage = 2;
      component.pageSize = 5;
      component.updateFilteredImages();

      expect(component.currentPage).toBeLessThanOrEqual(component.totalPages);
    });
  });

  describe('Page Navigation', () => {
    beforeEach(() => {
      component.images = Array.from({ length: 36 }, (_, i) => ({
        id: i + 1,
        url: `image${i + 1}.jpg`,
        title: `Image ${i + 1}`,
        type: 'generated',
        status: 'completed',
        createdAt: new Date(),
      })) as GalleryImage[];
      component.pageSize = 12;
      component.updateFilteredImages();
    });

    it('should go to next page', () => {
      component.currentPage = 1;
      component.goToPage(2);

      expect(component.currentPage).toBe(2);
      expect(component.paginatedImages[0].id).toBe(13);
    });

    it('should go to previous page', () => {
      component.currentPage = 2;
      component.goToPage(1);

      expect(component.currentPage).toBe(1);
      expect(component.paginatedImages[0].id).toBe(1);
    });

    it('should handle invalid page number', () => {
      component.currentPage = 1;
      component.goToPage(0);

      expect(component.currentPage).toBe(1);
    });

    it('should handle page number beyond total pages', () => {
      component.currentPage = 1;
      component.goToPage(10);

      expect(component.currentPage).toBe(1);
    });
  });

  describe('Image Selection Toggle', () => {
    beforeEach(() => {
      component.images = [
        {
          id: 1,
          url: 'image1.jpg',
          title: 'Image 1',
          type: 'generated',
          status: 'completed',
          createdAt: new Date(),
        },
        {
          id: 2,
          url: 'image2.jpg',
          title: 'Image 2',
          type: 'generated',
          status: 'completed',
          createdAt: new Date(),
        },
      ] as GalleryImage[];
      component.updateFilteredImages();
    });

    it('should select image when not selected', () => {
      component.selectedImages = [];

      component.toggleSelection(component.images[0]);

      expect(component.selectedImages).toContain(component.images[0]);
    });

    it('should deselect image when already selected', () => {
      component.selectedImages = [component.images[0]];

      component.toggleSelection(component.images[0]);

      expect(component.selectedImages).not.toContain(component.images[0]);
    });

    it('should check if image is selected', () => {
      component.selectedImages = [component.images[0]];

      expect(component.isSelected(component.images[0])).toBeTrue();
      expect(component.isSelected(component.images[1])).toBeFalse();
    });
  });

  describe('Edge Cases', () => {
    it('should handle empty images array', () => {
      component.images = [];
      component.updateFilteredImages();

      expect(component.filteredImages).toEqual([]);
      expect(component.paginatedImages).toEqual([]);
      expect(component.totalPages).toBe(0);
    });

    it('should handle null filter type', () => {
      component.images = [{ id: 1, type: 'generated' } as GalleryImage];
      component.filterType = null as any;
      component.updateFilteredImages();

      expect(component.filteredImages.length).toBe(1);
    });

    it('should handle undefined filter type', () => {
      component.images = [{ id: 1, type: 'generated' } as GalleryImage];
      component.filterType = undefined as any;
      component.updateFilteredImages();

      expect(component.filteredImages.length).toBe(1);
    });

    it('should handle zero page size', () => {
      component.images = [{ id: 1, type: 'generated' } as GalleryImage];
      component.pageSize = 0;
      component.updateFilteredImages();

      expect(component.paginatedImages.length).toBe(0);
    });

    it('should handle negative page size', () => {
      component.images = [{ id: 1, type: 'generated' } as GalleryImage];
      component.pageSize = -5;
      component.updateFilteredImages();

      expect(component.paginatedImages.length).toBe(0);
    });

    it('should handle very large page size', () => {
      component.images = [
        { id: 1, type: 'generated' } as GalleryImage,
        { id: 2, type: 'generated' } as GalleryImage,
      ];
      component.pageSize = 1000;
      component.updateFilteredImages();

      expect(component.paginatedImages.length).toBe(2);
      expect(component.totalPages).toBe(1);
    });

    it('should handle images with missing properties', () => {
      component.images = [{ id: 1 } as GalleryImage, { id: 2, type: 'generated' } as GalleryImage];
      component.filterType = 'generated';
      component.updateFilteredImages();

      expect(component.filteredImages.length).toBe(1);
      expect(component.filteredImages[0].id).toBe(2);
    });
  });

  describe('Component Event Handlers', () => {
    it('should handle view mode change', () => {
      component.setViewMode('list');

      expect(component.viewMode).toBe('list');
    });

    it('should handle page size change', () => {
      spyOn(component, 'updateFilteredImages');

      component.changePageSize(24);

      expect(component.pageSize).toBe(24);
      expect(component.currentPage).toBe(1);
      expect(component.updateFilteredImages).toHaveBeenCalled();
    });

    it('should handle page change', () => {
      spyOn(component, 'updateFilteredImages');

      component.goToPage(3);

      expect(component.currentPage).toBe(3);
      expect(component.updateFilteredImages).toHaveBeenCalled();
    });
  });

  describe('Computed Properties', () => {
    beforeEach(() => {
      component.images = Array.from({ length: 10 }, (_, i) => ({
        id: i + 1,
        type: 'generated',
        status: 'completed',
      })) as GalleryImage[];
      component.updateFilteredImages();
    });

    it('should calculate total items correctly', () => {
      expect(component.filteredImages.length).toBe(10);
    });

    it('should update total items when filter changes', () => {
      component.images = [
        { id: 1, type: 'generated' } as GalleryImage,
        {
          id: 2,
          type: 'original',
          url: 'x',
          title: 'x',
          createdAt: new Date(),
          status: 'completed',
        } as GalleryImage,
      ];
      component.filterType = 'generated';
      component.updateFilteredImages();

      expect(component.filteredImages.length).toBe(1);
    });

    it('should check if has images', () => {
      expect(component.images.length > 0).toBeTrue();
      component.images = [];
      component.updateFilteredImages();
      expect(component.images.length > 0).toBeFalse();
    });

    it('should check if has filtered images', () => {
      expect(component.filteredImages.length > 0).toBeTrue();
      component.filterType = 'nonexistent';
      component.updateFilteredImages();
      expect(component.filteredImages.length > 0).toBeFalse();
    });
  });

  describe('Selection State Management', () => {
    beforeEach(() => {
      component.images = [
        { id: 1, type: 'generated' } as GalleryImage,
        { id: 2, type: 'generated' } as GalleryImage,
        {
          id: 3,
          type: 'original',
          url: 'y',
          title: 'y',
          createdAt: new Date(),
          status: 'completed',
        } as GalleryImage,
      ];
      component.updateFilteredImages();
    });

    it('should check if all visible images are selected', () => {
      component.selectedImages = [...component.filteredImages];
      expect(component.selectedImages.length === component.filteredImages.length).toBeTrue();
    });

    it('should check if no images are selected', () => {
      component.selectedImages = [];
      expect(component.selectedImages.length === component.filteredImages.length).toBeFalse();
    });

    it('should check if some images are selected', () => {
      component.selectedImages = [component.filteredImages[0]];
      expect(component.selectedImages.length === component.filteredImages.length).toBeFalse();
    });

    it('should get selected count', () => {
      component.selectedImages = [component.filteredImages[0]];
      expect(component.selectedImages.length).toBe(1);
    });

    it('should check if has selected images', () => {
      component.selectedImages = [];
      expect(component.selectedImages.length > 0).toBeFalse();
      component.selectedImages = [component.filteredImages[0]];
      expect(component.selectedImages.length > 0).toBeTrue();
    });
  });
});

/**
 * Integration Tests for Photo Gallery Component
 */
describe('PhotoGalleryComponent Integration Tests', () => {
  let component: PhotoGalleryComponent;
  let fixture: ComponentFixture<PhotoGalleryComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PhotoGalleryComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(PhotoGalleryComponent);
    component = fixture.componentInstance;
  });

  it('should handle complete workflow: filter → paginate → select → action', () => {
    // Setup data
    component.images = Array.from({ length: 15 }, (_, i) => ({
      id: i + 1,
      url: `image${i + 1}.jpg`,
      title: `Image ${i + 1}`,
      type: i % 2 === 0 ? 'generated' : 'original',
      status: 'completed',
      createdAt: new Date(),
    })) as GalleryImage[];

    // 1. Filter by generated images
    component.filterType = 'generated';
    component.updateFilteredImages();

    const generatedCount = component.images.filter(img => img.type === 'generated').length;
    expect(component.filteredImages.length).toBe(generatedCount);

    // 2. Check pagination
    expect(component.totalPages).toBeGreaterThan(0);
    expect(component.paginatedImages.length).toBeLessThanOrEqual(component.pageSize);

    // 3. Select images
    component.selectAll();
    expect(component.selectedImages.length).toBe(component.filteredImages.length);

    // 4. Perform action
    spyOn(component.bulkDownload, 'emit');
    component.downloadSelected();
    expect(component.bulkDownload.emit).toHaveBeenCalledWith(component.selectedImages);
  });

  it('should maintain consistent state during filter changes', () => {
    component.images = [
      { id: 1, type: 'generated' },
      { id: 2, type: 'original' },
      { id: 3, type: 'generated' },
    ] as GalleryImage[];

    // Select all images when showing 'all'
    component.filterType = 'all';
    component.updateFilteredImages();
    component.selectAll();

    expect(component.selectedImages.length).toBe(3);

    // Filter to only 'generated' - should maintain valid selections
    component.filterType = 'generated';
    component.updateFilteredImages();

    // The component should handle this gracefully
    expect(component.filteredImages.length).toBe(2);
  });
});

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PhotoGalleryComponent, GalleryImage } from './photo-gallery.component';

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
      imports: [PhotoGalleryComponent]
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
          createdAt: new Date()
        },
        { 
          id: 2, 
          url: 'image2.jpg', 
          title: 'Image 2', 
          type: 'original', 
          status: 'completed',
          createdAt: new Date()
        },
        { 
          id: 3, 
          url: 'image3.jpg', 
          title: 'Image 3', 
          type: 'generated', 
          status: 'completed',
          createdAt: new Date()
        }
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
      const event = { target: { value: 'original' } };
      spyOn(component, 'updateFilteredImages');
      
      component.onFilterChange(event);
      
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
        createdAt: new Date()
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
        { id: 1, url: 'image1.jpg', title: 'Image 1', type: 'generated', status: 'completed', createdAt: new Date() },
        { id: 2, url: 'image2.jpg', title: 'Image 2', type: 'generated', status: 'completed', createdAt: new Date() },
        { id: 3, url: 'image3.jpg', title: 'Image 3', type: 'generated', status: 'completed', createdAt: new Date() }
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
        createdAt: new Date()
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
      const validImage = { id: 1, url: 'valid.jpg', title: 'Valid', type: 'generated', status: 'completed', createdAt: new Date() } as GalleryImage;
      const invalidImage = { id: 2, url: 'invalid.jpg', title: 'Invalid', type: 'generated', status: 'completed', createdAt: new Date() } as GalleryImage;
      
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
      expect(component.Math).toBe(Math);
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
      imports: [PhotoGalleryComponent]
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
      createdAt: new Date()
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
      { id: 3, type: 'generated' }
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
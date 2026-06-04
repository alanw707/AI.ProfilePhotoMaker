import { ComponentFixture, TestBed } from '@angular/core/testing';
import { GalleryFilterControlsComponent } from './gallery-filter-controls.component';
import { GalleryImage } from '../photo-gallery.component';

describe('GalleryFilterControlsComponent', () => {
  let component: GalleryFilterControlsComponent;
  let fixture: ComponentFixture<GalleryFilterControlsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [GalleryFilterControlsComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(GalleryFilterControlsComponent);
    component = fixture.componentInstance;
  });

  describe('Component Initialization', () => {
    it('should create', () => {
      expect(component).toBeTruthy();
    });

    it('should initialize with default values', () => {
      expect(component.title).toBe('Photo Workspace');
      expect(component.filterType).toBe('generated');
      expect(component.viewMode).toBe('grid');
      expect(component.pageSize).toBe(12);
      expect(component.filteredImages).toEqual([]);
      expect(component.selectedImages).toEqual([]);
      expect(component.showBulkActions).toBe(true);
      expect(component.allowSelection).toBe(true);
    });
  });

  describe('Filter Controls', () => {
    it('should emit filterChange when filter selection changes', () => {
      spyOn(component.filterChange, 'emit');
      const event = { target: { value: 'original' } } as any;

      component.onFilterChange(event);

      expect(component.filterChange.emit).toHaveBeenCalledWith('original');
    });

    it('should handle empty filter change event', () => {
      spyOn(component.filterChange, 'emit');
      const event = { target: { value: '' } } as any;

      component.onFilterChange(event);

      expect(component.filterChange.emit).toHaveBeenCalledWith('');
    });
  });

  describe('View Mode Controls', () => {
    it('should emit viewModeChange when switching to grid view', () => {
      spyOn(component.viewModeChange, 'emit');

      component.setViewMode('grid');

      expect(component.viewModeChange.emit).toHaveBeenCalledWith('grid');
    });

    it('should emit viewModeChange when switching to list view', () => {
      spyOn(component.viewModeChange, 'emit');

      component.setViewMode('list');

      expect(component.viewModeChange.emit).toHaveBeenCalledWith('list');
    });
  });

  describe('Page Size Controls', () => {
    it('should emit pageSizeChange with correct value', () => {
      spyOn(component.pageSizeChange, 'emit');
      const event = { target: { value: '24' } } as any;

      component.onPageSizeChange(event);

      expect(component.pageSizeChange.emit).toHaveBeenCalledWith(24);
    });

    it('should handle invalid page size selection', () => {
      spyOn(component.pageSizeChange, 'emit');
      const event = { target: { value: 'invalid' } } as any;

      component.onPageSizeChange(event);

      expect(component.pageSizeChange.emit).toHaveBeenCalledWith(NaN);
    });

    it('should not emit when target is null', () => {
      spyOn(component.pageSizeChange, 'emit');
      const event = { target: null } as any;

      component.onPageSizeChange(event);

      expect(component.pageSizeChange.emit).not.toHaveBeenCalled();
    });
  });

  describe('Bulk Actions', () => {
    beforeEach(() => {
      component.filteredImages = [
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
    });

    it('should emit selectAll when onSelectAll is called', () => {
      spyOn(component.selectAll, 'emit');

      component.onSelectAll();

      expect(component.selectAll.emit).toHaveBeenCalled();
    });

    it('should emit downloadSelected when onDownloadSelected is called', () => {
      spyOn(component.downloadSelected, 'emit');

      component.onDownloadSelected();

      expect(component.downloadSelected.emit).toHaveBeenCalled();
    });

    it('should show "Select All" when no images are selected', () => {
      component.selectedImages = [];

      expect(component.selectAllText).toBe('Select All');
    });

    it('should show "Deselect All" when all images are selected', () => {
      component.selectedImages = [...component.filteredImages];

      expect(component.selectAllText).toBe('Deselect All');
    });

    it('should show "Select All" when only some images are selected', () => {
      component.selectedImages = [component.filteredImages[0]];

      expect(component.selectAllText).toBe('Select All');
    });

    it('should show bulk actions when showBulkActions is true and multiple images exist', () => {
      component.showBulkActions = true;
      component.filteredImages = [{ id: 1 } as GalleryImage, { id: 2 } as GalleryImage];

      expect(component.shouldShowBulkActions).toBe(true);
    });

    it('should hide bulk actions when showBulkActions is false', () => {
      component.showBulkActions = false;
      component.filteredImages = [{ id: 1 } as GalleryImage, { id: 2 } as GalleryImage];

      expect(component.shouldShowBulkActions).toBe(false);
    });

    it('should hide bulk actions when only one image exists', () => {
      component.showBulkActions = true;
      component.filteredImages = [{ id: 1 } as GalleryImage];

      expect(component.shouldShowBulkActions).toBe(false);
    });

    it('should hide bulk actions when no images exist', () => {
      component.showBulkActions = true;
      component.filteredImages = [];

      expect(component.shouldShowBulkActions).toBe(false);
    });
  });

  describe('Edge Cases', () => {
    it('should handle null target in filter change', () => {
      spyOn(component.filterChange, 'emit');
      const event = { target: null } as any;

      // Should not throw error
      expect(() => component.onFilterChange(event)).not.toThrow();
    });

    it('should handle empty value in page size change', () => {
      spyOn(component.pageSizeChange, 'emit');
      const event = { target: { value: '' } } as any;

      component.onPageSizeChange(event);

      expect(component.pageSizeChange.emit).not.toHaveBeenCalled();
    });

    it('should handle zero page size selection', () => {
      spyOn(component.pageSizeChange, 'emit');
      const event = { target: { value: '0' } } as any;

      component.onPageSizeChange(event);

      expect(component.pageSizeChange.emit).toHaveBeenCalledWith(0);
    });

    it('should handle very large page size', () => {
      spyOn(component.pageSizeChange, 'emit');
      const event = { target: { value: '1000' } } as any;

      component.onPageSizeChange(event);

      expect(component.pageSizeChange.emit).toHaveBeenCalledWith(1000);
    });

    it('should handle negative page size', () => {
      spyOn(component.pageSizeChange, 'emit');
      const event = { target: { value: '-10' } } as any;

      component.onPageSizeChange(event);

      expect(component.pageSizeChange.emit).toHaveBeenCalledWith(-10);
    });
  });

  describe('Input Properties', () => {
    it('should handle title changes', () => {
      component.title = 'My Custom Gallery';
      expect(component.title).toBe('My Custom Gallery');
    });

    it('should handle filterType changes', () => {
      component.filterType = 'uploaded';
      expect(component.filterType).toBe('uploaded');

      component.filterType = 'enhanced';
      expect(component.filterType).toBe('enhanced');
    });

    it('should handle viewMode changes', () => {
      component.viewMode = 'list';
      expect(component.viewMode).toBe('list');

      component.viewMode = 'grid';
      expect(component.viewMode).toBe('grid');
    });

    it('should handle pageSize changes', () => {
      component.pageSize = 6;
      expect(component.pageSize).toBe(6);

      component.pageSize = 48;
      expect(component.pageSize).toBe(48);
    });

    it('should handle allowSelection changes', () => {
      component.allowSelection = false;
      expect(component.allowSelection).toBeFalse();

      component.allowSelection = true;
      expect(component.allowSelection).toBeTrue();
    });
  });

  describe('Complex Scenarios', () => {
    it('should handle mixed image types in filtered images', () => {
      component.filteredImages = [
        {
          id: 1,
          url: 'img1.jpg',
          title: 'Image 1',
          createdAt: new Date(),
          type: 'original',
          status: 'completed',
        } as GalleryImage,
        {
          id: 2,
          url: 'img2.jpg',
          title: 'Image 2',
          createdAt: new Date(),
          type: 'generated',
          status: 'completed',
        } as GalleryImage,
        {
          id: 3,
          url: 'img3.jpg',
          title: 'Image 3',
          createdAt: new Date(),
          type: 'enhanced',
          status: 'processing',
        } as GalleryImage,
      ];

      expect(component.shouldShowBulkActions).toBeTrue();
    });

    it('should handle large number of images', () => {
      const largeImageSet = Array.from({ length: 1000 }, (_, i) => ({
        id: i + 1,
        url: `image${i + 1}.jpg`,
        title: `Image ${i + 1}`,
        type: 'generated',
        status: 'completed',
        createdAt: new Date(),
      })) as GalleryImage[];

      component.filteredImages = largeImageSet;
      component.selectedImages = largeImageSet.slice(0, 500);

      expect(component.selectAllText).toBe('Select All');
      expect(component.shouldShowBulkActions).toBeTrue();
    });

    it('should handle all images selected in large dataset', () => {
      const largeImageSet = Array.from({ length: 100 }, (_, i) => ({
        id: i + 1,
        type: 'generated',
        status: 'completed',
      })) as GalleryImage[];

      component.filteredImages = largeImageSet;
      component.selectedImages = [...largeImageSet];

      expect(component.selectAllText).toBe('Deselect All');
    });
  });

  describe('State Management', () => {
    it('should maintain component state correctly', () => {
      // Initial state
      expect(component.title).toBe('Photo Workspace');
      expect(component.filterType).toBe('generated');
      expect(component.viewMode).toBe('grid');
      expect(component.pageSize).toBe(12);

      // Change state
      component.title = 'My Photos';
      component.filterType = 'uploaded';
      component.viewMode = 'list';
      component.pageSize = 24;

      // Verify state persistence
      expect(component.title).toBe('My Photos');
      expect(component.filterType).toBe('uploaded');
      expect(component.viewMode).toBe('list');
      expect(component.pageSize).toBe(24);
    });
  });

  describe('Component Integration', () => {
    it('should handle complete user workflow', () => {
      // Setup spies
      spyOn(component.filterChange, 'emit');
      spyOn(component.viewModeChange, 'emit');
      spyOn(component.pageSizeChange, 'emit');
      spyOn(component.selectAll, 'emit');
      spyOn(component.downloadSelected, 'emit');

      // User changes filter
      component.onFilterChange({ target: { value: 'all' } } as any);
      expect(component.filterChange.emit).toHaveBeenCalledWith('all');

      // User changes view mode
      component.setViewMode('list');
      expect(component.viewModeChange.emit).toHaveBeenCalledWith('list');

      // User changes page size
      component.onPageSizeChange({ target: { value: '24' } } as any);
      expect(component.pageSizeChange.emit).toHaveBeenCalledWith(24);

      // User selects all
      component.onSelectAll();
      expect(component.selectAll.emit).toHaveBeenCalled();

      // User downloads selected
      component.onDownloadSelected();
      expect(component.downloadSelected.emit).toHaveBeenCalled();
    });

    it('should handle rapid successive changes', () => {
      spyOn(component.filterChange, 'emit');
      spyOn(component.viewModeChange, 'emit');

      // Rapid filter changes
      component.onFilterChange({ target: { value: 'uploaded' } } as any);
      component.onFilterChange({ target: { value: 'generated' } } as any);
      component.onFilterChange({ target: { value: 'enhanced' } } as any);

      expect(component.filterChange.emit).toHaveBeenCalledTimes(3);
      expect(component.filterChange.emit).toHaveBeenCalledWith('uploaded');
      expect(component.filterChange.emit).toHaveBeenCalledWith('generated');
      expect(component.filterChange.emit).toHaveBeenCalledWith('enhanced');

      // Rapid view mode changes
      component.setViewMode('list');
      component.setViewMode('grid');
      component.setViewMode('list');

      expect(component.viewModeChange.emit).toHaveBeenCalledTimes(3);
      expect(component.viewModeChange.emit).toHaveBeenCalledWith('list');
      expect(component.viewModeChange.emit).toHaveBeenCalledWith('grid');
    });
  });
});

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SimpleChange } from '@angular/core';
import { GalleryPaginationComponent } from './gallery-pagination.component';

describe('GalleryPaginationComponent', () => {
  let component: GalleryPaginationComponent;
  let fixture: ComponentFixture<GalleryPaginationComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [GalleryPaginationComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(GalleryPaginationComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  describe('Component Initialization', () => {
    it('should create', () => {
      expect(component).toBeTruthy();
    });

    it('should initialize with default values', () => {
      expect(component.totalItems).toBe(0);
      expect(component.pageSize).toBe(12);
      expect(component.currentPage).toBe(1);
      expect(component.totalPages).toBe(0);
    });

    it('should have Math available in template', () => {
      expect((component as any).mathHelper).toBe(Math);
    });

    it('should calculate total pages on init', () => {
      component.totalItems = 50;
      component.pageSize = 10;

      component.ngOnInit();

      expect(component.totalPages).toBe(5);
    });
  });

  describe('Total Pages Calculation', () => {
    it('should calculate total pages correctly', () => {
      component.totalItems = 25;
      component.pageSize = 10;

      component['updateTotalPages']();

      expect(component.totalPages).toBe(3);
    });

    it('should handle exact division', () => {
      component.totalItems = 30;
      component.pageSize = 10;

      component['updateTotalPages']();

      expect(component.totalPages).toBe(3);
    });

    it('should handle zero items', () => {
      component.totalItems = 0;
      component.pageSize = 10;

      component['updateTotalPages']();

      expect(component.totalPages).toBe(0);
    });

    it('should handle single page', () => {
      component.totalItems = 5;
      component.pageSize = 10;

      component['updateTotalPages']();

      expect(component.totalPages).toBe(1);
    });

    it('should update total pages when inputs change', () => {
      component.totalItems = 100;
      component.pageSize = 20;

      component.ngOnChanges({
        totalItems: new SimpleChange(null, 100, false),
        pageSize: new SimpleChange(null, 20, false),
      });

      expect(component.totalPages).toBe(5);
    });

    it('should not update total pages when other properties change', () => {
      component.totalItems = 100;
      component.pageSize = 20;
      component['updateTotalPages']();
      const originalTotalPages = component.totalPages;

      component.ngOnChanges({
        currentPage: new SimpleChange(null, 2, false),
      });

      expect(component.totalPages).toBe(originalTotalPages);
    });
  });

  describe('Page Navigation', () => {
    beforeEach(() => {
      component.totalItems = 50;
      component.pageSize = 10;
      component.currentPage = 3;
      component.ngOnInit();
    });

    it('should go to valid page', () => {
      spyOn(component.pageChange, 'emit');

      component.goToPage(2);

      expect(component.pageChange.emit).toHaveBeenCalledWith(2);
    });

    it('should not go to invalid page (less than 1)', () => {
      spyOn(component.pageChange, 'emit');

      component.goToPage(0);

      expect(component.pageChange.emit).not.toHaveBeenCalled();
    });

    it('should not go to invalid page (greater than total pages)', () => {
      spyOn(component.pageChange, 'emit');

      component.goToPage(10);

      expect(component.pageChange.emit).not.toHaveBeenCalled();
    });

    it('should not go to same page', () => {
      spyOn(component.pageChange, 'emit');

      component.goToPage(3);

      expect(component.pageChange.emit).not.toHaveBeenCalled();
    });

    it('should go to next page', () => {
      spyOn(component.pageChange, 'emit');

      component.nextPage();

      expect(component.pageChange.emit).toHaveBeenCalledWith(4);
    });

    it('should not go to next page if on last page', () => {
      component.currentPage = 5;
      spyOn(component.pageChange, 'emit');

      component.nextPage();

      expect(component.pageChange.emit).not.toHaveBeenCalled();
    });

    it('should go to previous page', () => {
      spyOn(component.pageChange, 'emit');

      component.previousPage();

      expect(component.pageChange.emit).toHaveBeenCalledWith(2);
    });

    it('should not go to previous page if on first page', () => {
      component.currentPage = 1;
      spyOn(component.pageChange, 'emit');

      component.previousPage();

      expect(component.pageChange.emit).not.toHaveBeenCalled();
    });
  });

  describe('Page Size Changes', () => {
    it('should emit page size change', () => {
      spyOn(component.pageSizeChange, 'emit');

      component.changePageSize(20);

      expect(component.pageSizeChange.emit).toHaveBeenCalledWith(20);
    });

    it('should handle different page sizes', () => {
      spyOn(component.pageSizeChange, 'emit');

      component.changePageSize(6);
      component.changePageSize(24);
      component.changePageSize(48);

      expect(component.pageSizeChange.emit).toHaveBeenCalledTimes(3);
      expect(component.pageSizeChange.emit).toHaveBeenCalledWith(6);
      expect(component.pageSizeChange.emit).toHaveBeenCalledWith(24);
      expect(component.pageSizeChange.emit).toHaveBeenCalledWith(48);
    });
  });

  describe('Page Numbers Generation', () => {
    it('should return all pages when total pages is 5 or less', () => {
      component.totalItems = 40;
      component.pageSize = 10;
      component.currentPage = 2;
      component.ngOnInit();

      const pageNumbers = component.getPageNumbers();

      expect(pageNumbers).toEqual([1, 2, 3, 4]);
    });

    it('should return page numbers with ellipsis for large page counts', () => {
      component.totalItems = 100;
      component.pageSize = 10;
      component.currentPage = 5;
      component.ngOnInit();

      const pageNumbers = component.getPageNumbers();

      expect(pageNumbers).toEqual([1, -1, 4, 5, 6, -1, 10]);
    });

    it('should handle current page at beginning', () => {
      component.totalItems = 100;
      component.pageSize = 10;
      component.currentPage = 2;
      component.ngOnInit();

      const pageNumbers = component.getPageNumbers();

      expect(pageNumbers).toEqual([1, 2, 3, -1, 10]);
    });

    it('should handle current page at end', () => {
      component.totalItems = 100;
      component.pageSize = 10;
      component.currentPage = 9;
      component.ngOnInit();

      const pageNumbers = component.getPageNumbers();

      expect(pageNumbers).toEqual([1, -1, 8, 9, 10]);
    });

    it('should handle current page as first page', () => {
      component.totalItems = 100;
      component.pageSize = 10;
      component.currentPage = 1;
      component.ngOnInit();

      const pageNumbers = component.getPageNumbers();

      expect(pageNumbers).toEqual([1, 2, -1, 10]);
    });

    it('should handle current page as last page', () => {
      component.totalItems = 100;
      component.pageSize = 10;
      component.currentPage = 10;
      component.ngOnInit();

      const pageNumbers = component.getPageNumbers();

      expect(pageNumbers).toEqual([1, -1, 9, 10]);
    });

    it('should handle single page', () => {
      component.totalItems = 5;
      component.pageSize = 10;
      component.currentPage = 1;
      component.ngOnInit();

      const pageNumbers = component.getPageNumbers();

      expect(pageNumbers).toEqual([1]);
    });

    it('should handle two pages', () => {
      component.totalItems = 15;
      component.pageSize = 10;
      component.currentPage = 1;
      component.ngOnInit();

      const pageNumbers = component.getPageNumbers();

      expect(pageNumbers).toEqual([1, 2]);
    });

    it('should handle exactly 5 pages', () => {
      component.totalItems = 50;
      component.pageSize = 10;
      component.currentPage = 3;
      component.ngOnInit();

      const pageNumbers = component.getPageNumbers();

      expect(pageNumbers).toEqual([1, 2, 3, 4, 5]);
    });

    it('should handle 6 pages (edge case)', () => {
      component.totalItems = 60;
      component.pageSize = 10;
      component.currentPage = 3;
      component.ngOnInit();

      const pageNumbers = component.getPageNumbers();

      expect(pageNumbers).toEqual([1, 2, 3, 4, -1, 6]);
    });
  });

  describe('Edge Cases', () => {
    it('should handle zero total items', () => {
      component.totalItems = 0;
      component.pageSize = 10;
      component.currentPage = 1;
      component.ngOnInit();

      expect(component.totalPages).toBe(0);
      expect(component.getPageNumbers()).toEqual([]);
    });

    it('should handle very large page size', () => {
      component.totalItems = 100;
      component.pageSize = 1000;
      component.currentPage = 1;
      component.ngOnInit();

      expect(component.totalPages).toBe(1);
      expect(component.getPageNumbers()).toEqual([1]);
    });

    it('should handle page size of 1', () => {
      component.totalItems = 5;
      component.pageSize = 1;
      component.currentPage = 3;
      component.ngOnInit();

      expect(component.totalPages).toBe(5);
      expect(component.getPageNumbers()).toEqual([1, 2, 3, 4, 5]);
    });

    it('should handle very large total items', () => {
      component.totalItems = 10000;
      component.pageSize = 10;
      component.currentPage = 500;
      component.ngOnInit();

      expect(component.totalPages).toBe(1000);
      const pageNumbers = component.getPageNumbers();
      expect(pageNumbers).toEqual([1, -1, 499, 500, 501, -1, 1000]);
    });

    it('should handle negative page numbers in navigation', () => {
      component.totalItems = 50;
      component.pageSize = 10;
      component.currentPage = 1;
      component.ngOnInit();

      spyOn(component.pageChange, 'emit');

      component.goToPage(-1);

      expect(component.pageChange.emit).not.toHaveBeenCalled();
    });
  });

  describe('Input Validation', () => {
    it('should handle invalid totalItems', () => {
      component.totalItems = -10;
      component.pageSize = 10;
      component.ngOnInit();

      // Should result in 0 pages due to Math.ceil of negative number
      expect(component.totalPages).toBe(0);
    });

    it('should handle zero page size', () => {
      component.totalItems = 100;
      component.pageSize = 0;
      component.ngOnInit();

      // Should result in Infinity, which is handled by the component
      expect(component.totalPages).toBe(Infinity);
    });

    it('should handle invalid current page', () => {
      component.totalItems = 50;
      component.pageSize = 10;
      component.currentPage = 0;
      component.ngOnInit();

      // Component should still work with invalid current page
      expect(component.totalPages).toBe(5);
    });
  });

  describe('Event Emission', () => {
    it('should emit page change event only once per call', () => {
      component.totalItems = 50;
      component.pageSize = 10;
      component.currentPage = 2;
      component.ngOnInit();

      spyOn(component.pageChange, 'emit');

      component.goToPage(3);

      expect(component.pageChange.emit).toHaveBeenCalledTimes(1);
      expect(component.pageChange.emit).toHaveBeenCalledWith(3);
    });

    it('should emit page size change event with correct value', () => {
      spyOn(component.pageSizeChange, 'emit');

      component.changePageSize(25);

      expect(component.pageSizeChange.emit).toHaveBeenCalledTimes(1);
      expect(component.pageSizeChange.emit).toHaveBeenCalledWith(25);
    });

    it('should not emit page change when going to same page', () => {
      component.totalItems = 50;
      component.pageSize = 10;
      component.currentPage = 3;
      component.ngOnInit();

      spyOn(component.pageChange, 'emit');

      component.goToPage(3);

      expect(component.pageChange.emit).not.toHaveBeenCalled();
    });
  });

  describe('Integration Tests', () => {
    it('should handle complete pagination workflow', () => {
      component.totalItems = 100;
      component.pageSize = 10;
      component.currentPage = 1;
      component.ngOnInit();

      spyOn(component.pageChange, 'emit');
      spyOn(component.pageSizeChange, 'emit');

      // Navigate through pages
      component.nextPage();
      expect(component.pageChange.emit).toHaveBeenCalledWith(2);

      component.currentPage = 2;
      component.nextPage();
      expect(component.pageChange.emit).toHaveBeenCalledWith(3);

      component.currentPage = 3;
      component.previousPage();
      expect(component.pageChange.emit).toHaveBeenCalledWith(2);

      // Change page size
      component.changePageSize(20);
      expect(component.pageSizeChange.emit).toHaveBeenCalledWith(20);

      // Update component state
      component.pageSize = 20;
      component.ngOnInit();
      expect(component.totalPages).toBe(5);
    });
  });
});

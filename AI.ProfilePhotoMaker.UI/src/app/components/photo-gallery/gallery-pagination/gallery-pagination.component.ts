import { Component, EventEmitter, Input, OnChanges, OnInit, Output, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-gallery-pagination',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './gallery-pagination.component.html',
  styleUrls: ['./gallery-pagination.component.sass']
})
export class GalleryPaginationComponent implements OnInit, OnChanges {
  @Input() totalItems = 0;
  @Input() pageSize = 12;
  @Input() currentPage = 1;
  
  @Output() pageChange = new EventEmitter<number>();
  @Output() pageSizeChange = new EventEmitter<number>();

  // Make Math available in template
  Math = Math;

  totalPages = 1;

  ngOnInit() {
    this.updateTotalPages();
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['totalItems'] || changes['pageSize']) {
      this.updateTotalPages();
    }
  }

  private updateTotalPages() {
    this.totalPages = Math.ceil(this.totalItems / this.pageSize);
  }

  goToPage(page: number) {
    if (page >= 1 && page <= this.totalPages && page !== this.currentPage) {
      this.pageChange.emit(page);
    }
  }

  nextPage() {
    if (this.currentPage < this.totalPages) {
      this.goToPage(this.currentPage + 1);
    }
  }

  previousPage() {
    if (this.currentPage > 1) {
      this.goToPage(this.currentPage - 1);
    }
  }

  changePageSize(size: number) {
    this.pageSizeChange.emit(size);
  }

  getPageNumbers(): number[] {
    const pages: number[] = [];
    const maxVisible = 5;
    
    if (this.totalPages <= maxVisible) {
      for (let i = 1; i <= this.totalPages; i++) {
        pages.push(i);
      }
    } else {
      pages.push(1);
      
      if (this.currentPage > 3) {
        pages.push(-1); // Ellipsis
      }
      
      const start = Math.max(2, this.currentPage - 1);
      const end = Math.min(this.totalPages - 1, this.currentPage + 1);
      
      for (let i = start; i <= end; i++) {
        if (i !== 1 && i !== this.totalPages) {
          pages.push(i);
        }
      }
      
      if (this.currentPage < this.totalPages - 2) {
        pages.push(-1); // Ellipsis
      }
      
      if (this.totalPages > 1) {
        pages.push(this.totalPages);
      }
    }
    
    return pages;
  }
}
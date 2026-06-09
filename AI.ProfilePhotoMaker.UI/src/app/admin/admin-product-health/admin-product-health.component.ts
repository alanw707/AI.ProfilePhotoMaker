import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AdminProductHealthDto, AdminService } from '../../services/admin.service';

@Component({
  selector: 'app-admin-product-health',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './admin-product-health.component.html',
  styleUrls: ['../admin-shared.sass', './admin-product-health.component.sass'],
})
export class AdminProductHealthComponent implements OnInit {
  readonly windows = [
    { label: '24h', value: '24h' },
    { label: '7d', value: '7d' },
    { label: '30d', value: '30d' },
    { label: 'All-time', value: 'all' },
  ];

  selectedWindow = '7d';
  productHealth: AdminProductHealthDto | null = null;
  isLoading = false;
  error: string | null = null;

  constructor(private readonly adminService: AdminService) {}

  ngOnInit(): void {
    this.loadProductHealth();
  }

  selectWindow(window: string): void {
    if (this.selectedWindow === window) {
      return;
    }

    this.selectedWindow = window;
    this.loadProductHealth();
  }

  loadProductHealth(): void {
    this.isLoading = true;
    this.error = null;

    this.adminService.getProductHealth(this.selectedWindow).subscribe({
      next: data => {
        this.productHealth = data;
        this.isLoading = false;
      },
      error: err => {
        console.error('Failed to load product health:', err);
        this.error = err?.message || 'Failed to load product health. Please try again.';
        this.isLoading = false;
      },
    });
  }

  formatRate(value: number | null | undefined): string {
    if (value === null || value === undefined) {
      return '0%';
    }

    return `${Math.round(value * 1000) / 10}%`;
  }
}

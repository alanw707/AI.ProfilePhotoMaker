import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { Subject } from 'rxjs';
import { takeUntil, timeout } from 'rxjs/operators';
import { AdminService } from '../../services/admin.service';

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './admin-dashboard.component.html',
  styleUrls: ['../admin-shared.sass', './admin-dashboard.component.sass'],
})
export class AdminDashboardComponent implements OnInit, OnDestroy {
  dashboard = {
    totalUsers: 0,
    activeUsers: 0,
    totalCreditsOutstanding: 0,
    totalCreditsPurchased: 0,
    activeCoupons: 0,
  };

  isLoading = false;
  error: string | null = null;
  private destroy$ = new Subject<void>();

  constructor(private _adminService: AdminService) {}

  ngOnInit(): void {
    this.loadDashboard();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadDashboard(): void {
    this.isLoading = true;
    this.error = null;
    this._adminService
      .getDashboard()
      .pipe(timeout(15000), takeUntil(this.destroy$))
      .subscribe({
        next: data => {
          this.dashboard = data;
          this.isLoading = false;
        },
        error: err => {
          console.error('Failed to load dashboard:', err);
          this.error = err?.message || 'Failed to load dashboard statistics. Please try again.';
          this.isLoading = false;
        },
      });
  }
}

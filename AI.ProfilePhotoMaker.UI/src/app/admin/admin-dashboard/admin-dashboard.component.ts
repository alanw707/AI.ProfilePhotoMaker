import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AdminService } from '../../services/admin.service';

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './admin-dashboard.component.html',
  styleUrls: ['../admin-shared.sass', './admin-dashboard.component.sass'],
})
export class AdminDashboardComponent implements OnInit {
  dashboard = {
    totalUsers: 0,
    activeUsers: 0,
    totalCreditsOutstanding: 0,
    totalCreditsPurchased: 0,
    activeCoupons: 0,
  };

  constructor(private _adminService: AdminService) {}

  ngOnInit(): void {
    this._adminService.getDashboard().subscribe({
      next: data => {
        this.dashboard = data;
      },
    });
  }
}

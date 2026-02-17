import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { AdminService } from '../../services/admin.service';

@Component({
  selector: 'app-admin-user-detail',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './admin-user-detail.component.html',
})
export class AdminUserDetailComponent implements OnInit {
  user: any;

  constructor(
    private _route: ActivatedRoute,
    private _adminService: AdminService
  ) {}

  ngOnInit(): void {
    const userId = this._route.snapshot.paramMap.get('userId');
    if (!userId) {
      return;
    }

    this._adminService.getUserDetail(userId).subscribe({
      next: data => {
        this.user = data;
      },
    });
  }
}

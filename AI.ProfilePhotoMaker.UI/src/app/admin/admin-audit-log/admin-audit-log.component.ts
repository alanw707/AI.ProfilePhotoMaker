import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { AdminService } from '../../services/admin.service';

@Component({
  selector: 'app-admin-audit-log',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './admin-audit-log.component.html',
  styleUrls: ['../admin-shared.sass', './admin-audit-log.component.sass'],
})
export class AdminAuditLogComponent implements OnInit {
  logs: any[] = [];
  actionFilter = '';
  page = 1;
  pageSize = 50;
  expanded: Record<number, boolean> = {};

  constructor(private _adminService: AdminService) {}

  ngOnInit(): void {
    this.loadLogs();
  }

  loadLogs(): void {
    this._adminService.getAuditLogs(this.page, this.pageSize, this.actionFilter).subscribe({
      next: data => {
        this.logs = data.logs || [];
      },
    });
  }

  toggleDetails(id: number): void {
    this.expanded[id] = !this.expanded[id];
  }

  preview(details: string | null | undefined): string {
    if (!details) {
      return '-';
    }

    return details.length <= 80 ? details : `${details.substring(0, 80)}...`;
  }
}

import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { AdminService } from '../../services/admin.service';
import { finalize } from 'rxjs/operators';

@Component({
  selector: 'app-admin-coupons',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './admin-coupons.component.html',
  styleUrls: ['../admin-shared.sass', './admin-coupons.component.sass'],
})
export class AdminCouponsComponent implements OnInit {
  coupons: any[] = [];
  model = {
    code: '',
    discountType: 1,
    discountValue: 10,
    maxUsages: 100,
    expiresAt: '',
  };
  warning = '';
  error: string | null = null;
  isLoading = false;
  isSubmitting = false;
  editCouponId: number | null = null;
  editModel = {
    maxUsages: 0,
    expiresAt: '',
    isActive: true,
  };
  confirmDeleteId: number | null = null;

  constructor(private _adminService: AdminService) {}

  ngOnInit(): void {
    this.loadCoupons();
  }

  loadCoupons(): void {
    this.isLoading = true;
    this.error = null;
    this._adminService
      .getCoupons()
      .pipe(finalize(() => (this.isLoading = false)))
      .subscribe({
        next: data => {
          this.coupons = data || [];
        },
        error: err => (this.error = err?.message || 'Failed to load coupons. Please retry.'),
      });
  }

  createCoupon(): void {
    if (this.isSubmitting) {
      return;
    }
    this.warning = '';
    this.error = null;
    const payload = {
      code: this.model.code.trim(),
      discountType: this.model.discountType,
      discountValue: this.model.discountValue,
      maxUsages: this.model.maxUsages,
      expiresAt: this.model.expiresAt ? new Date(this.model.expiresAt).toISOString() : null,
    };

    this.isSubmitting = true;
    this._adminService
      .createCoupon(payload)
      .pipe(finalize(() => (this.isSubmitting = false)))
      .subscribe({
        next: response => {
          this.warning = response.warning || '';
          this.loadCoupons();
        },
        error: err => (this.error = err?.message || 'Failed to create coupon.'),
      });
  }

  deleteCoupon(id: number): void {
    this.confirmDeleteId = id;
  }

  confirmDelete(): void {
    if (!this.confirmDeleteId) {
      return;
    }

    this._adminService.deleteCoupon(this.confirmDeleteId).subscribe(() => {
      this.confirmDeleteId = null;
      this.loadCoupons();
    });
  }

  cancelDelete(): void {
    this.confirmDeleteId = null;
  }

  startEdit(coupon: any): void {
    this.editCouponId = coupon.id;
    this.editModel = {
      maxUsages: coupon.maxUsages,
      expiresAt: coupon.expiresAt ? this.toInputDate(coupon.expiresAt) : '',
      isActive: coupon.isActive,
    };
  }

  saveEdit(): void {
    if (!this.editCouponId) {
      return;
    }

    this._adminService
      .updateCoupon(this.editCouponId, {
        maxUsages: this.editModel.maxUsages,
        expiresAt: this.editModel.expiresAt
          ? new Date(this.editModel.expiresAt).toISOString()
          : null,
        isActive: this.editModel.isActive,
      })
      .subscribe(() => {
        this.editCouponId = null;
        this.loadCoupons();
      });
  }

  cancelEdit(): void {
    this.editCouponId = null;
  }

  private toInputDate(value: string): string {
    const date = new Date(value);
    const yyyy = date.getFullYear();
    const mm = String(date.getMonth() + 1).padStart(2, '0');
    const dd = String(date.getDate()).padStart(2, '0');
    const hh = String(date.getHours()).padStart(2, '0');
    const min = String(date.getMinutes()).padStart(2, '0');
    return `${yyyy}-${mm}-${dd}T${hh}:${min}`;
  }
}

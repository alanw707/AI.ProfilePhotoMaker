import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { UserProfile } from '../../../services/profile.service';

@Component({
  selector: 'app-account-info',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './account-info.component.html',
  styleUrl: './account-info.component.sass'
})
export class AccountInfoComponent {
  @Input() userEmail = '';
  @Input() userProfile: UserProfile | null = null;
  @Input() accountAge = 0;

  @Output() editProfileClicked = new EventEmitter<void>();

  getFullName(): string {
    if (!this.userProfile) return '';
    return `${this.userProfile.firstName} ${this.userProfile.lastName}`.trim();
  }

  formatDate(date: any): string {
    if (!date) return '';
    const d = new Date(date);
    return d.toLocaleDateString('en-US', { 
      year: 'numeric', 
      month: 'long', 
      day: 'numeric' 
    });
  }

  editProfile(): void {
    this.editProfileClicked.emit();
  }
}

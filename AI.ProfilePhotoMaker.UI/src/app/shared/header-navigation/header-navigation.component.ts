import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { ThemeService } from '../../services/theme.service';
import { CreditService, UserCreditStatus } from '../../services/credit.service';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-header-navigation',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './header-navigation.component.html',
  styleUrls: ['./header-navigation.component.sass'],
})
export class HeaderNavigationComponent implements OnInit, OnDestroy {
  userName = '';
  userEmail = '';
  userCreditStatus: UserCreditStatus | null = null;
  isMobileMenuOpen = false;
  private userSubscription?: Subscription;
  private creditSubscription?: Subscription;

  constructor(
    private authService: AuthService,
    private router: Router,
    public themeService: ThemeService,
    private creditService: CreditService
  ) {}

  ngOnInit() {
    this.userSubscription = this.authService.currentUser$.subscribe(user => {
      if (user) {
        this.userEmail = user.email;
        this.userName =
          `${user.firstName || ''} ${user.lastName || ''}`.trim() || this.userEmail.split('@')[0];

        // Only load credit status when authenticated
        this.loadCreditStatus();
      } else {
        // Clear credit status when not authenticated
        this.userCreditStatus = null;
      }
    });
  }

  ngOnDestroy() {
    if (this.userSubscription) {
      this.userSubscription.unsubscribe();
    }
    if (this.creditSubscription) {
      this.creditSubscription.unsubscribe();
    }
  }

  loadCreditStatus() {
    this.creditSubscription = this.creditService.getCreditStatus().subscribe({
      next: response => {
        if (response.success) {
          this.userCreditStatus = response.data;
        }
      },
      error: error => {
        console.error('Failed to load credit status:', error);
      },
    });
  }

  toggleTheme() {
    this.themeService.toggleTheme();
  }

  logout() {
    this.authService.logout();
    this.router.navigate(['/login']);
  }

  toggleMobileMenu() {
    this.isMobileMenuOpen = !this.isMobileMenuOpen;
  }
}

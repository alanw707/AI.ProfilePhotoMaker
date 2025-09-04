import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  OnDestroy,
  OnInit,
} from '@angular/core';
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
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HeaderNavigationComponent implements OnInit, OnDestroy {
  userName = '';
  userEmail = '';
  userCreditStatus: UserCreditStatus | null = null;
  isMobileMenuOpen = false;
  isAuthenticated = false;
  private _userSubscription?: Subscription;
  private _creditSubscription?: Subscription;
  private _authSubscription?: Subscription;

  constructor(
    private _authService: AuthService,
    private _router: Router,
    public themeService: ThemeService,
    private _creditService: CreditService,
    private _cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    // Track authentication state explicitly to control header UI
    this._authSubscription = this._authService.isAuthenticated$.subscribe(isAuth => {
      this.isAuthenticated = isAuth;
      this._cdr.markForCheck();
    });

    this._userSubscription = this._authService.currentUser$.subscribe(user => {
      if (user) {
        this.userEmail = user.email;
        // Prefer first + last; then email prefix; finally a safe placeholder
        const fullName = `${user.firstName || ''} ${user.lastName || ''}`.trim();
        const emailPrefix = (this.userEmail || '').includes('@')
          ? this.userEmail.split('@')[0]
          : (this.userEmail || '').trim();
        this.userName = fullName || emailPrefix || 'User';

        // Only load credit status when authenticated - add small delay for auth token
        setTimeout(() => {
          this.loadCreditStatus();
        }, 100);
        this._cdr.markForCheck();
      } else {
        // Clear credit status when not authenticated
        this.userCreditStatus = null;
        this.userName = '';
        this.userEmail = '';
        this._cdr.markForCheck();
      }
    });
  }

  ngOnDestroy(): void {
    if (this._userSubscription) {
      this._userSubscription.unsubscribe();
    }
    if (this._creditSubscription) {
      this._creditSubscription.unsubscribe();
    }
    if (this._authSubscription) {
      this._authSubscription.unsubscribe();
    }
  }

  loadCreditStatus(): void {
    this._creditSubscription = this._creditService.getCreditStatus().subscribe({
      next: response => {
        if (response.success) {
          this.userCreditStatus = response.data;
          // Force change detection to update the view
          this._cdr.detectChanges();
        } else {
          this.userCreditStatus = null;
          this._cdr.detectChanges();
        }
      },
      error: error => {
        console.error('Failed to load credit status:', error);
        this.userCreditStatus = null;
        this._cdr.detectChanges();
      },
    });
  }

  toggleTheme(): void {
    this.themeService.toggleTheme();
  }

  logout(): void {
    this._authService.logout();
    // Navigation handled by auth service
  }

  toggleMobileMenu(): void {
    this.isMobileMenuOpen = !this.isMobileMenuOpen;
    // Control body scrolling when mobile menu is open
    document.body.style.overflow = this.isMobileMenuOpen ? 'hidden' : 'auto';
  }

  closeMobileMenu(): void {
    this.isMobileMenuOpen = false;
    // Always restore body scrolling when menu closes
    document.body.style.overflow = 'auto';
  }
}

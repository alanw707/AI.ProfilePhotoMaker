import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  OnDestroy,
  OnInit,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { NavigationEnd, Router, RouterModule } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { ThemeService } from '../../services/theme.service';
import { UserCreditStatus } from '../../services/credit.service';
import { SubscriptionStateService } from '../../services/subscription-state.service';
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
  isHeadshotContext = false;
  private _userSubscription?: Subscription;
  private _authSubscription?: Subscription;
  private _subscriptionStateSubscription?: Subscription;
  private _routerSubscription?: Subscription;

  constructor(
    private _authService: AuthService,
    private _router: Router,
    public themeService: ThemeService,
    private _subscriptionState: SubscriptionStateService,
    private _cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.isHeadshotContext = this._router.url.includes('/app/enhance');
    this._routerSubscription = this._router.events.subscribe(event => {
      if (event instanceof NavigationEnd) {
        this.isHeadshotContext = event.urlAfterRedirects.includes('/app/enhance');
        this._cdr.markForCheck();
      }
    });

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

        // Ensure subscription state has current credits (small delay for cookie/session settle)
        setTimeout(() => {
          void this._subscriptionState.refreshInternalCredits();
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

    this._subscriptionStateSubscription = this._subscriptionState.state$.subscribe(state => {
      this.userCreditStatus = this.isAuthenticated ? state.userCreditStatus : null;
      this._cdr.markForCheck();
    });
  }

  ngOnDestroy(): void {
    if (this._userSubscription) {
      this._userSubscription.unsubscribe();
    }
    if (this._authSubscription) {
      this._authSubscription.unsubscribe();
    }
    if (this._subscriptionStateSubscription) {
      this._subscriptionStateSubscription.unsubscribe();
    }
    if (this._routerSubscription) {
      this._routerSubscription.unsubscribe();
    }
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

  getHeaderCreditLabel(): string {
    return 'Credits';
  }

  getHeaderCreditCount(): number {
    if (!this.userCreditStatus) {
      return 0;
    }

    return this.userCreditStatus.credits || 0;
  }
}

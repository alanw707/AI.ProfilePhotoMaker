import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ElementRef,
  HostListener,
  Input,
  OnDestroy,
  OnInit,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { NavigationEnd, Router, RouterModule } from '@angular/router';
import { Subscription } from 'rxjs';
import { AuthService } from '../../services/auth.service';
import { NavigationService } from '../../services/navigation.service';

type MarketingMenu = 'features' | 'use-cases' | 'compare' | null;

@Component({
  selector: 'app-marketing-header',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './marketing-header.component.html',
  styleUrls: ['./marketing-header.component.sass'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MarketingHeaderComponent implements OnInit, OnDestroy {
  @Input() queryParams: Record<string, string> | null = null;
  @Input() isLanding = false;

  isAuthenticated = false;
  isMobileMenuOpen = false;
  activeMenu: MarketingMenu = null;

  private readonly subscriptions = new Subscription();

  constructor(
    private readonly authService: AuthService,
    private readonly router: Router,
    private readonly navigationService: NavigationService,
    private readonly cdr: ChangeDetectorRef,
    private readonly elementRef: ElementRef<HTMLElement>
  ) {}

  ngOnInit(): void {
    this.subscriptions.add(
      this.authService.isAuthenticated$.subscribe(isAuth => {
        this.isAuthenticated = isAuth;
        this.cdr.markForCheck();
      })
    );

    this.subscriptions.add(
      this.router.events.subscribe(event => {
        if (event instanceof NavigationEnd) {
          this.closeAllMenus();
        }
      })
    );
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
    this.resetBodyScroll();
  }

  toggleMobileMenu(): void {
    this.isMobileMenuOpen = !this.isMobileMenuOpen;
    if (!this.isMobileMenuOpen) {
      this.activeMenu = null;
    }
    this.syncBodyScroll();
  }

  closeMobileMenu(): void {
    this.isMobileMenuOpen = false;
    this.activeMenu = null;
    this.syncBodyScroll();
  }

  toggleMenu(menu: MarketingMenu): void {
    if (!this.isCompactNav()) {
      return;
    }
    this.activeMenu = this.activeMenu === menu ? null : menu;
  }

  openMenu(menu: MarketingMenu): void {
    if (this.isCompactNav()) {
      return;
    }
    this.activeMenu = menu;
  }

  closeMenu(menu: MarketingMenu): void {
    if (this.isCompactNav()) {
      return;
    }
    if (this.activeMenu === menu) {
      this.activeMenu = null;
    }
  }

  closeAllMenus(): void {
    this.activeMenu = null;
    this.isMobileMenuOpen = false;
    this.resetBodyScroll();
  }

  isMenuOpen(menu: MarketingMenu): boolean {
    return this.activeMenu === menu;
  }

  logout(): void {
    this.authService.logout();
  }

  navigateToPricing(): void {
    if (this.isLanding) {
      void this.navigationService.navigateToSection('pricing');
      this.closeAllMenus();
      return;
    }
    void this.router.navigate(['/pricing'], {
      queryParams: this.getPricingParams() || undefined,
      fragment: 'packages-section',
    });
    this.closeAllMenus();
  }

  navigateToReviews(): void {
    if (this.isLanding) {
      void this.navigationService.navigateToSection('testimonials');
      this.closeAllMenus();
      return;
    }
    void this.router.navigate(['/reviews']);
    this.closeAllMenus();
  }

  navigateToDashboard(): void {
    void this.router.navigate(['/app/dashboard']);
    this.closeAllMenus();
  }

  getPricingParams(): Record<string, string> | null {
    return this.queryParams && Object.keys(this.queryParams).length > 0 ? this.queryParams : null;
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!this.activeMenu && !this.isMobileMenuOpen) {
      return;
    }
    const target = event.target as Node | null;
    if (target && this.elementRef.nativeElement.contains(target)) {
      return;
    }
    this.closeAllMenus();
    this.cdr.markForCheck();
  }

  @HostListener('document:focusin', ['$event'])
  onDocumentFocus(event: FocusEvent): void {
    if (!this.activeMenu && !this.isMobileMenuOpen) {
      return;
    }
    const target = event.target as Node | null;
    if (target && this.elementRef.nativeElement.contains(target)) {
      return;
    }
    this.closeAllMenus();
    this.cdr.markForCheck();
  }

  @HostListener('document:keydown', ['$event'])
  onDocumentKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      this.closeAllMenus();
      this.cdr.markForCheck();
    }
  }

  private isCompactNav(): boolean {
    if (typeof window === 'undefined') {
      return false;
    }
    return window.matchMedia('(max-width: 960px)').matches;
  }

  private syncBodyScroll(): void {
    document.body.style.overflow = this.isMobileMenuOpen ? 'hidden' : 'auto';
  }

  private resetBodyScroll(): void {
    document.body.style.overflow = 'auto';
  }
}

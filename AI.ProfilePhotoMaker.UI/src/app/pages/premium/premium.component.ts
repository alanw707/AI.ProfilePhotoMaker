import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { ThemeService } from '../../services/theme.service';
import { PremiumPackageSelectionComponent } from '../../components/premium-package-selection/premium-package-selection.component';
import { PremiumPackageService, PremiumPackage, UserPackageStatus } from '../../services/premium-package.service';
import { NotificationService } from '../../services/notification.service';

@Component({
  selector: 'app-premium',
  standalone: true,
  imports: [CommonModule, RouterModule, PremiumPackageSelectionComponent],
  template: `
    <div class="premium-page-container">
      <!-- Theme Toggle Button -->
      <button class="theme-toggle" (click)="toggleTheme()" [attr.aria-label]="'Switch to ' + (themeService.isDark() ? 'light' : 'dark') + ' theme'">
        <svg *ngIf="!themeService.isDark()" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M20.354 15.354A9 9 0 018.646 3.646 9.003 9.003 0 0012 21a9.003 9.003 0 008.354-5.646z"/>
        </svg>
        <svg *ngIf="themeService.isDark()" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 3v1m0 16v1m9-9h-1M4 12H3m15.364 6.364l-.707-.707M6.343 6.343l-.707-.707m12.728 0l-.707.707M6.343 17.657l-.707.707M16 12a4 4 0 11-8 0 4 4 0 018 0z"/>
        </svg>
      </button>

      <!-- Navigation Header -->
      <header class="dashboard-header">
        <div class="header-content">
          <div class="logo-section">
            <img src="Logo.PNG" alt="AI Profile Photo Maker" class="header-logo">
            <h1>AI Profile Photo Maker</h1>
          </div>
          
          <!-- Navigation Menu -->
          <nav class="nav-menu">
            <a routerLink="/packages" routerLinkActive="active" class="nav-link">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
                <path d="M12 2L3.09 8.26L12 22L20.91 8.26L12 2Z" stroke="currentColor" stroke-width="2" fill="none"/>
              </svg>
              Packages
            </a>
            <a routerLink="/dashboard" routerLinkActive="active" [routerLinkActiveOptions]="{exact: true}" class="nav-link">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
                <rect x="3" y="3" width="7" height="9" stroke="currentColor" stroke-width="2"/>
                <rect x="13" y="3" width="8" height="5" stroke="currentColor" stroke-width="2"/>
                <rect x="13" y="12" width="8" height="9" stroke="currentColor" stroke-width="2"/>
                <rect x="3" y="16" width="7" height="5" stroke="currentColor" stroke-width="2"/>
              </svg>
              Studio
            </a>
            <a routerLink="/enhance" routerLinkActive="active" class="nav-link">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
                <circle cx="12" cy="12" r="3" stroke="currentColor" stroke-width="2"/>
                <path d="M12 1v6m0 6v6m11-7h-6m-6 0H1" stroke="currentColor" stroke-width="2"/>
              </svg>
              Enhance
            </a>
            <a routerLink="/gallery" routerLinkActive="active" class="nav-link">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
                <rect x="3" y="3" width="18" height="18" rx="2" ry="2" stroke="currentColor" stroke-width="2"/>
                <circle cx="8.5" cy="8.5" r="1.5" stroke="currentColor" stroke-width="2"/>
                <polyline points="21,15 16,10 5,21" stroke="currentColor" stroke-width="2"/>
              </svg>
              Gallery
            </a>
          </nav>

          <div class="user-section">
            <div class="user-info">
              <span class="user-name">{{userName}}</span>
              <span class="user-email">{{userEmail}}</span>
            </div>
            <button class="btn btn-logout" (click)="logout()">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
                <path d="M16 17L21 12M21 12L16 7M21 12H9M9 21H5C3.89543 21 3 20.1046 3 19V5C3 3.89543 3.89543 3 5 3H9" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
              </svg>
              Logout
            </button>
          </div>
        </div>
      </header>

      <!-- Main Premium Content -->
      <main class="premium-main">
        <div class="premium-content">
          <!-- Hero Section -->
          <section class="hero-section">
            <div class="hero-content">
              <h1>Unlock Professional AI Photos</h1>
              <p class="hero-subtitle">Create stunning, personalized profile photos with our advanced AI technology. Train your own custom model for unlimited professional results.</p>
              
              <div class="features-grid">
                <div class="feature-item">
                  <div class="feature-icon">🎯</div>
                  <h3>Custom AI Training</h3>
                  <p>Train a personalized AI model with your photos for authentic, professional results</p>
                </div>
                <div class="feature-item">
                  <div class="feature-icon">⚡</div>
                  <h3>Multiple Styles</h3>
                  <p>Generate photos in various professional styles - corporate, creative, casual, and more</p>
                </div>
                <div class="feature-item">
                  <div class="feature-icon">🔒</div>
                  <h3>Privacy First</h3>
                  <p>Your trained model expires after 7 days for complete privacy and data protection</p>
                </div>
                <div class="feature-item">
                  <div class="feature-icon">📸</div>
                  <h3>High Quality</h3>
                  <p>Professional-grade images perfect for LinkedIn, resumes, and business profiles</p>
                </div>
              </div>
            </div>
          </section>

          <!-- Package Selection Section -->
          <section class="packages-section">
            <app-premium-package-selection 
              (packageSelected)="onPackageSelected($event)"
              (packagePurchased)="onPackagePurchased($event)">
            </app-premium-package-selection>
          </section>

          <!-- Divider -->
          <div class="section-divider"></div>

          <!-- How It Works Section -->
          <section class="how-it-works-section">
            <div class="section-header">
              <h2>How It Works</h2>
              <p>Get professional AI photos in just 5 simple steps</p>
            </div>

            <div class="steps-grid">
              <div class="step-item">
                <div class="step-number">1</div>
                <div class="step-content">
                  <h3>Choose Package</h3>
                  <p>Select the package that fits your needs and budget</p>
                </div>
              </div>
              <div class="step-item">
                <div class="step-number">2</div>
                <div class="step-content">
                  <h3>Upload Photos</h3>
                  <p>Upload 10-20 high-quality selfies for the best AI training results</p>
                </div>
              </div>
              <div class="step-item">
                <div class="step-number">3</div>
                <div class="step-content">
                  <h3>Select Styles</h3>
                  <p>Choose from professional, creative, casual, and formal photo styles</p>
                </div>
              </div>
              <div class="step-item">
                <div class="step-number">4</div>
                <div class="step-content">
                  <h3>AI Training</h3>
                  <p>Our AI trains a custom model with your photos (15-25 minutes)</p>
                </div>
              </div>
              <div class="step-item">
                <div class="step-number">5</div>
                <div class="step-content">
                  <h3>Download Photos</h3>
                  <p>Get your professional AI-generated photos ready for use</p>
                </div>
              </div>
            </div>
          </section>

          <!-- Already Have Package Section -->
          <section class="existing-package-section" *ngIf="userPackageStatus?.hasActivePackage">
            <div class="existing-package-card">
              <div class="package-status">
                <h3>🎯 You Have an Active Package!</h3>
                <div class="status-details">
                  <span class="package-name">{{ userPackageStatus?.packageName }}</span>
                  <span class="credits-remaining">{{ userPackageStatus?.creditsRemaining }} credits remaining</span>
                </div>
              </div>
              <button class="btn btn-primary" routerLink="/dashboard">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
                  <rect x="3" y="3" width="7" height="9" stroke="currentColor" stroke-width="2"/>
                  <rect x="13" y="3" width="8" height="5" stroke="currentColor" stroke-width="2"/>
                  <rect x="13" y="12" width="8" height="9" stroke="currentColor" stroke-width="2"/>
                  <rect x="3" y="16" width="7" height="5" stroke="currentColor" stroke-width="2"/>
                </svg>
                Go to Studio
              </button>
            </div>
          </section>
        </div>
      </main>
    </div>
  `,
  styleUrls: ['./premium.component.sass']
})
export class PremiumComponent implements OnInit {
  userName: string = '';
  userEmail: string = '';
  userPackageStatus: UserPackageStatus | null = null;

  constructor(
    private authService: AuthService,
    private router: Router,
    public themeService: ThemeService,
    private premiumPackageService: PremiumPackageService,
    private notificationService: NotificationService
  ) {}

  ngOnInit() {
    console.log('Premium page ngOnInit');
    
    // Check authentication first
    if (!this.authService.isAuthenticated()) {
      console.log('Not authenticated, redirecting to login');
      this.router.navigate(['/login']);
      return;
    }
    
    this.loadUserInfo();
    this.loadPremiumPackageStatus();
  }

  loadUserInfo() {
    // Get user info from auth service
    this.authService.currentUser$.subscribe(user => {
      if (user) {
        this.userEmail = user.email;
        
        // Use firstName/lastName from JWT if available, otherwise use email prefix
        const jwtName = `${user.firstName || ''} ${user.lastName || ''}`.trim();
        if (jwtName) {
          this.userName = jwtName;
        } else {
          // Fallback to email username if no JWT names
          this.userName = this.userEmail.split('@')[0];
        }
      }
    });
  }

  loadPremiumPackageStatus() {
    this.premiumPackageService.getUserPackageStatus().subscribe({
      next: (response) => {
        if (response.success) {
          this.userPackageStatus = response.data;
          
          // If user already has an active package, they might want to see their status
          // but still allow them to purchase additional packages if needed
        }
      },
      error: (error) => {
        console.error('Failed to load premium package status:', error);
        // User might not have a package yet, that's fine for this page
      }
    });
  }

  onPackageSelected(pkg: PremiumPackage) {
    console.log('Package selected:', pkg);
    // Package selection handled by the premium-package-selection component
  }

  onPackagePurchased(packageStatus: UserPackageStatus) {
    console.log('Package purchased:', packageStatus);
    this.userPackageStatus = packageStatus;
    
    this.notificationService.success('Premium Package Purchased!', 
      'Welcome to Premium! You can now access the Premium Studio to create professional photos.');
    
    // Redirect to dashboard to start the workflow
    this.router.navigate(['/dashboard']);
  }

  toggleTheme() {
    this.themeService.toggleTheme();
  }

  logout() {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { ThemeService } from '../../services/theme.service';
import { HeaderNavigationComponent } from '../../shared/header-navigation/header-navigation.component';
import { CreditPackagesComponent } from '../../components/credit-packages/credit-packages.component';
import { CreditService, UserCreditStatus } from '../../services/credit.service';
import { NotificationService } from '../../services/notification.service';

@Component({
  selector: 'app-premium',
  standalone: true,
  imports: [CommonModule, RouterModule, HeaderNavigationComponent, CreditPackagesComponent],
  template: `
    <div class="premium-page-container">
      <!-- Shared Header Navigation -->
      <app-header-navigation></app-header-navigation>

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
                  <p>Input photos deleted after 7 days, AI headshots stored 30 days. Full data control in account settings.</p>
                </div>
                <div class="feature-item">
                  <div class="feature-icon">📸</div>
                  <h3>High Quality</h3>
                  <p>Professional-grade images perfect for LinkedIn, resumes, and business profiles</p>
                </div>
              </div>
            </div>
          </section>

          <!-- Credit Packages Section -->
          <section class="packages-section">
            <app-credit-packages 
              (packagePurchased)="onCreditPackagePurchased($event)">
            </app-credit-packages>
          </section>

          <!-- Divider -->
          <div class="section-divider"></div>

          <!-- How It Works Section -->
          <section class="how-it-works-section">
            <div class="section-header">
              <h2>How It Works</h2>
              <p>Get professional AI photos in just 4 simple steps</p>
            </div>

            <div class="steps-grid">
              <div class="step-item">
                <div class="step-number">1</div>
                <div class="step-content">
                  <h3>Upload Photos</h3>
                  <p>Upload 10-20 high-quality selfies for the best AI training results</p>
                </div>
              </div>
              <div class="step-item">
                <div class="step-number">2</div>
                <div class="step-content">
                  <h3>Select Styles</h3>
                  <p>Choose from professional, creative, casual, and formal photo styles</p>
                </div>
              </div>
              <div class="step-item">
                <div class="step-number">3</div>
                <div class="step-content">
                  <h3>AI Training</h3>
                  <p>Our AI trains a custom model with your photos (15-25 minutes)</p>
                </div>
              </div>
              <div class="step-item">
                <div class="step-number">4</div>
                <div class="step-content">
                  <h3>Download Photos</h3>
                  <p>Get your professional AI-generated photos ready for use</p>
                </div>
              </div>
            </div>
          </section>

          <!-- Already Have Credits Section -->
          <section class="existing-package-section" *ngIf="userCreditStatus && (userCreditStatus.purchasedCredits > 0 || userCreditStatus.weeklyCredits > 0)">
            <div class="existing-package-card">
              <div class="package-status">
                <h3>🎯 You Have Credits Available!</h3>
                <div class="status-details">
                  <span class="package-name">Credit Balance</span>
                  <span class="credits-remaining">{{ userCreditStatus.totalCredits }} total credits available</span>
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
  userCreditStatus: UserCreditStatus | null = null;

  constructor(
    private authService: AuthService,
    private router: Router,
    private creditService: CreditService,
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
    
    this.loadCreditStatus();
  }


  loadCreditStatus() {
    this.creditService.getCreditStatus().subscribe({
      next: (response) => {
        if (response.success) {
          this.userCreditStatus = response.data;
        }
      },
      error: (error) => {
        console.error('Failed to load credit status:', error);
        // User might not have credits yet, that's fine for this page
      }
    });
  }

  onCreditPackagePurchased(creditStatus: UserCreditStatus) {
    console.log('Credit package purchased:', creditStatus);
    this.userCreditStatus = creditStatus;
    
    this.notificationService.success('Credits Purchased!', 
      'Credits added to your account! You can now use premium features like model training and styled generation.');
    
    // Redirect to dashboard to start the workflow
    this.router.navigate(['/dashboard']);
  }

}
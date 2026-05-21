import { inject } from '@angular/core';
import { Router, Routes } from '@angular/router';
import { LoginComponent } from './auth/login/login.component';
import { RegisterComponent } from './auth/register/register.component';
import { guestGuard } from './guards/guest.guard';
import { AppGuard } from './guards/app.guard';
import { AdminGuard } from './guards/admin.guard';
import { seoPages } from './pages/marketing/seo-pages.data';

export const routes: Routes = [
  // Home/Landing Page - Main entry point
  {
    path: '',
    loadComponent: () => import('./pages/landing/landing.component').then(m => m.LandingComponent),
    pathMatch: 'full',
    title: 'AI Profile Photo Maker - Score, Improve, and Export Your Best Profile Photo',
    data: {
      meta: {
        description:
          'Score, improve, and export a LinkedIn-ready professional profile photo from one upload.',
        keywords:
          'AI profile photo score, professional headshot package, LinkedIn photo export, profile photo workflow',
      },
    },
  },
  // Alternative home routes for SEO
  {
    path: 'home',
    redirectTo: '',
    pathMatch: 'full',
  },

  // Authentication Routes
  {
    path: 'auth',
    children: [
      {
        path: 'login',
        component: LoginComponent,
        canActivate: [guestGuard],
        title: 'Login - AI Profile Photo Maker',
        data: { hideNavigation: false },
      },
      {
        path: 'register',
        component: RegisterComponent,
        canActivate: [guestGuard],
        title: 'Sign Up - AI Profile Photo Maker',
        data: { hideNavigation: false },
      },
      {
        path: 'signup',
        redirectTo: 'register',
        pathMatch: 'full',
      },
      {
        path: 'complete-profile',
        loadComponent: () =>
          import('./auth/complete-profile/complete-profile.component').then(
            m => m.CompleteProfileComponent
          ),
        title: 'Complete Profile - AI Profile Photo Maker',
        data: { hideNavigation: false },
      },
      {
        path: 'confirm-email',
        loadComponent: () =>
          import('./auth/confirm-email/confirm-email.component').then(m => m.ConfirmEmailComponent),
        title: 'Confirm Email - AI Profile Photo Maker',
        data: { hideNavigation: false },
      },
      {
        path: 'verify-email',
        loadComponent: () =>
          import('./auth/verify-email/verify-email.component').then(m => m.VerifyEmailComponent),
        title: 'Verify Email - AI Profile Photo Maker',
        data: { hideNavigation: false },
      },
    ],
  },
  // Legacy auth routes (for backwards compatibility)
  {
    path: 'login',
    redirectTo: 'auth/login',
    pathMatch: 'full',
  },
  {
    path: 'register',
    redirectTo: 'auth/register',
    pathMatch: 'full',
  },
  {
    path: 'signup',
    redirectTo: 'auth/register',
    pathMatch: 'full',
  },

  // Protected Application Routes
  {
    path: 'app',
    canActivateChild: [AppGuard],
    children: [
      {
        path: '',
        redirectTo: 'enhance',
        pathMatch: 'full',
      },
      {
        path: 'enhance',
        loadComponent: () =>
          import('./components/photo-enhancement/photo-enhancement.component').then(
            m => m.PhotoEnhancementComponent
          ),
        title: 'Photo Workspace - AI Profile Photo Maker',
        data: {
          breadcrumb: 'Photo Workspace',
          hideNavigation: false,
        },
      },
      {
        path: 'gallery',
        loadComponent: () =>
          import('./pages/gallery/gallery.component').then(m => m.GalleryComponent),
        title: 'Photo Workspace Gallery - AI Profile Photo Maker',
        data: {
          breadcrumb: 'Photo Workspace',
          hideNavigation: false,
        },
      },
      {
        path: 'settings',
        loadComponent: () =>
          import('./pages/settings/settings.component').then(m => m.SettingsComponent),
        title: 'Settings - AI Profile Photo Maker',
        data: {
          breadcrumb: 'Settings',
          hideNavigation: false,
        },
      },
      {
        path: 'support',
        loadComponent: () =>
          import('./pages/support/support.component').then(m => m.SupportComponent),
        title: 'Support - AI Profile Photo Maker',
        data: {
          breadcrumb: 'Support',
          hideNavigation: false,
        },
      },
    ],
  },
  // Legacy protected routes (for backwards compatibility)
  {
    path: 'dashboard',
    canActivate: [
      (): Promise<boolean> => {
        const router = inject(Router);
        const queryParams = new URLSearchParams(window.location.search);
        const params: Record<string, string> = {};
        queryParams.forEach((value, key) => {
          params[key] = value;
        });
        return router.navigate(['/app/enhance'], { queryParams: params });
      },
    ],
    pathMatch: 'full',
  },
  {
    path: 'enhance',
    redirectTo: 'app/enhance',
    pathMatch: 'full',
  },
  {
    path: 'gallery',
    redirectTo: 'app/gallery',
    pathMatch: 'full',
  },
  {
    path: 'settings',
    redirectTo: 'app/settings',
    pathMatch: 'full',
  },

  // Public Marketing Pages
  {
    path: 'how-it-works',
    loadComponent: () =>
      import('./pages/marketing/seo-page/seo-page.component').then(m => m.SeoPageComponent),
    title: seoPages['how-it-works'].title,
    data: {
      seoPage: seoPages['how-it-works'],
    },
  },
  {
    path: 'examples',
    loadComponent: () =>
      import('./pages/marketing/seo-page/seo-page.component').then(m => m.SeoPageComponent),
    title: seoPages['examples'].title,
    data: {
      seoPage: seoPages['examples'],
    },
  },
  {
    path: 'reviews',
    redirectTo: '/',
    pathMatch: 'full',
  },
  {
    path: 'corporate-headshot',
    loadComponent: () =>
      import('./pages/marketing/seo-page/seo-page.component').then(m => m.SeoPageComponent),
    title: seoPages['corporate-headshot'].title,
    data: {
      seoPage: seoPages['corporate-headshot'],
    },
  },
  {
    path: 'free-headshot-enhancer',
    redirectTo: 'corporate-headshot',
    pathMatch: 'full',
  },
  {
    path: 'ai-headshot-generator',
    loadComponent: () =>
      import('./pages/marketing/seo-page/seo-page.component').then(m => m.SeoPageComponent),
    title: seoPages['ai-headshot-generator'].title,
    data: {
      seoPage: seoPages['ai-headshot-generator'],
    },
  },
  {
    path: 'linkedin-headshots',
    loadComponent: () =>
      import('./pages/marketing/seo-page/seo-page.component').then(m => m.SeoPageComponent),
    title: seoPages['linkedin-headshots'].title,
    data: {
      seoPage: seoPages['linkedin-headshots'],
    },
  },
  {
    path: 'professional-headshots',
    loadComponent: () =>
      import('./pages/marketing/seo-page/seo-page.component').then(m => m.SeoPageComponent),
    title: seoPages['professional-headshots'].title,
    data: {
      seoPage: seoPages['professional-headshots'],
    },
  },
  {
    path: 'headshots-for-job-search',
    loadComponent: () =>
      import('./pages/marketing/seo-page/seo-page.component').then(m => m.SeoPageComponent),
    title: seoPages['headshots-for-job-search'].title,
    data: {
      seoPage: seoPages['headshots-for-job-search'],
    },
  },
  {
    path: 'realtor-headshots',
    loadComponent: () =>
      import('./pages/marketing/seo-page/seo-page.component').then(m => m.SeoPageComponent),
    title: seoPages['realtor-headshots'].title,
    data: {
      seoPage: seoPages['realtor-headshots'],
    },
  },
  {
    path: 'lawyer-headshots',
    loadComponent: () =>
      import('./pages/marketing/seo-page/seo-page.component').then(m => m.SeoPageComponent),
    title: seoPages['lawyer-headshots'].title,
    data: {
      seoPage: seoPages['lawyer-headshots'],
    },
  },
  {
    path: 'doctor-headshots',
    loadComponent: () =>
      import('./pages/marketing/seo-page/seo-page.component').then(m => m.SeoPageComponent),
    title: seoPages['doctor-headshots'].title,
    data: {
      seoPage: seoPages['doctor-headshots'],
    },
  },
  {
    path: 'compare',
    children: [
      {
        path: 'aragon-ai',
        loadComponent: () =>
          import('./pages/marketing/seo-page/seo-page.component').then(m => m.SeoPageComponent),
        title: seoPages['compare-aragon-ai'].title,
        data: {
          seoPage: seoPages['compare-aragon-ai'],
        },
      },
      {
        path: 'headshotpro',
        loadComponent: () =>
          import('./pages/marketing/seo-page/seo-page.component').then(m => m.SeoPageComponent),
        title: seoPages['compare-headshotpro'].title,
        data: {
          seoPage: seoPages['compare-headshotpro'],
        },
      },
    ],
  },
  {
    path: 'pricing',
    loadComponent: () => import('./pages/premium/premium.component').then(m => m.PremiumComponent),
    title: 'Pricing Plans - AI Profile Photo Maker',
    data: {
      meta: {
        description:
          'Choose the perfect plan for your AI profile photo needs. Simple, transparent pricing with no hidden fees.',
        keywords: 'AI photo pricing, profile photo plans, professional headshot cost',
      },
    },
  },
  {
    path: 'packages',
    redirectTo: 'pricing',
    pathMatch: 'full',
  },

  // Blog
  {
    path: 'blog',
    loadComponent: () => import('./pages/blog/blog-list.component').then(m => m.BlogListComponent),
    title: 'Blog - AI Profile Photo Maker',
  },
  {
    path: 'blog/:slug',
    loadComponent: () => import('./pages/blog/blog-post.component').then(m => m.BlogPostComponent),
    title: 'Blog Post - AI Profile Photo Maker',
  },

  // Legal Pages
  {
    path: 'legal',
    children: [
      {
        path: 'privacy',
        loadComponent: () =>
          import('./pages/privacy/privacy.component').then(m => m.PrivacyComponent),
        title: 'Privacy Policy - AI Profile Photo Maker',
      },
      {
        path: 'terms',
        loadComponent: () => import('./pages/terms/terms.component').then(m => m.TermsComponent),
        title: 'Terms of Service - AI Profile Photo Maker',
      },
      {
        path: 'retention-policy',
        loadComponent: () =>
          import('./pages/retention-policy/retention-policy.component').then(
            m => m.RetentionPolicyComponent
          ),
        title: 'Data Retention Policy - AI Profile Photo Maker',
      },
      {
        path: 'refund-policy',
        loadComponent: () =>
          import('./pages/refund-policy/refund-policy.component').then(
            m => m.RefundPolicyComponent
          ),
        title: 'Refund Policy - AI Profile Photo Maker',
      },
      {
        path: 'cookies',
        loadComponent: () =>
          import('./pages/cookies/cookies.component').then(m => m.CookiesComponent),
        title: 'Cookie Policy - AI Profile Photo Maker',
      },
      {
        path: 'subprocessors',
        loadComponent: () =>
          import('./pages/subprocessors/subprocessors.component').then(
            m => m.SubprocessorsComponent
          ),
        title: 'Subprocessors - AI Profile Photo Maker',
      },
      {
        path: 'ai-transparency',
        loadComponent: () =>
          import('./pages/ai-transparency/ai-transparency.component').then(
            m => m.AiTransparencyComponent
          ),
        title: 'AI Transparency - AI Profile Photo Maker',
      },
      {
        path: 'acceptable-use',
        loadComponent: () =>
          import('./pages/acceptable-use/acceptable-use.component').then(
            m => m.AcceptableUseComponent
          ),
        title: 'Acceptable Use Policy - AI Profile Photo Maker',
      },
      {
        path: 'ip-dmca',
        loadComponent: () =>
          import('./pages/ip-dmca/ip-dmca.component').then(m => m.IpDmcaComponent),
        title: 'IP / DMCA Policy - AI Profile Photo Maker',
      },
      {
        path: 'security',
        loadComponent: () =>
          import('./pages/security/security.component').then(m => m.SecurityComponent),
        title: 'Security & Trust - AI Profile Photo Maker',
      },
      {
        path: 'biometric-consent',
        loadComponent: () =>
          import('./pages/biometric-consent/biometric-consent.component').then(
            m => m.BiometricConsentComponent
          ),
        title: 'Biometric Consent - AI Profile Photo Maker',
      },
      {
        path: 'children-privacy',
        loadComponent: () =>
          import('./pages/children-privacy/children-privacy.component').then(
            m => m.ChildrenPrivacyComponent
          ),
        title: "Children's Privacy - AI Profile Photo Maker",
      },
    ],
  },
  // Legacy legal routes (for backwards compatibility)
  {
    path: 'privacy',
    redirectTo: 'legal/privacy',
    pathMatch: 'full',
  },
  {
    path: 'terms',
    redirectTo: 'legal/terms',
    pathMatch: 'full',
  },

  // SEO-friendly feature routes
  {
    path: 'features',
    loadComponent: () =>
      import('./pages/marketing/seo-page/seo-page.component').then(m => m.SeoPageComponent),
    title: seoPages['features'].title,
    data: {
      seoPage: seoPages['features'],
    },
  },
  {
    path: 'dating-app-headshots',
    loadComponent: () =>
      import('./pages/marketing/seo-page/seo-page.component').then(m => m.SeoPageComponent),
    title: seoPages['dating-app-headshots'].title,
    data: {
      seoPage: seoPages['dating-app-headshots'],
    },
  },
  {
    path: 'real-estate-agent-headshots',
    loadComponent: () =>
      import('./pages/marketing/seo-page/seo-page.component').then(m => m.SeoPageComponent),
    title: seoPages['real-estate-agent-headshots'].title,
    data: {
      seoPage: seoPages['real-estate-agent-headshots'],
    },
  },
  {
    path: 'medical-professional-headshots',
    loadComponent: () =>
      import('./pages/marketing/seo-page/seo-page.component').then(m => m.SeoPageComponent),
    title: seoPages['medical-professional-headshots'].title,
    data: {
      seoPage: seoPages['medical-professional-headshots'],
    },
  },
  {
    path: 'nurse-headshots',
    loadComponent: () =>
      import('./pages/marketing/seo-page/seo-page.component').then(m => m.SeoPageComponent),
    title: seoPages['nurse-headshots'].title,
    data: {
      seoPage: seoPages['nurse-headshots'],
    },
  },
  {
    path: 'teacher-headshots',
    loadComponent: () =>
      import('./pages/marketing/seo-page/seo-page.component').then(m => m.SeoPageComponent),
    title: seoPages['teacher-headshots'].title,
    data: {
      seoPage: seoPages['teacher-headshots'],
    },
  },

  // Help & Support
  {
    path: 'help',
    children: [
      {
        path: '',
        loadComponent: () =>
          import('./pages/marketing/seo-page/seo-page.component').then(m => m.SeoPageComponent),
        title: seoPages['help'].title,
        data: {
          seoPage: seoPages['help'],
        },
      },
      {
        path: 'faq',
        redirectTo: '',
        pathMatch: 'full',
      },
    ],
  },

  // Admin Routes (protected by AdminGuard)
  {
    path: 'admin',
    canActivate: [AdminGuard],
    canActivateChild: [AdminGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      {
        path: 'dashboard',
        loadComponent: () =>
          import('./admin/admin-dashboard/admin-dashboard.component').then(
            m => m.AdminDashboardComponent
          ),
        title: 'Admin Dashboard',
      },
      {
        path: 'users',
        loadComponent: () =>
          import('./admin/admin-users/admin-users.component').then(m => m.AdminUsersComponent),
        title: 'User Management',
      },
      {
        path: 'users/:userId',
        loadComponent: () =>
          import('./admin/admin-user-detail/admin-user-detail.component').then(
            m => m.AdminUserDetailComponent
          ),
        title: 'User Detail',
      },
      {
        path: 'coupons',
        loadComponent: () =>
          import('./admin/admin-coupons/admin-coupons.component').then(
            m => m.AdminCouponsComponent
          ),
        title: 'Coupon Management',
      },
      {
        path: 'audit-log',
        loadComponent: () =>
          import('./admin/admin-audit-log/admin-audit-log.component').then(
            m => m.AdminAuditLogComponent
          ),
        title: 'Audit Log',
      },
      {
        path: 'campaigns',
        loadComponent: () =>
          import('./admin/admin-campaigns/admin-campaigns.component').then(
            m => m.AdminCampaignsComponent
          ),
        title: 'Email Campaigns',
      },
    ],
  },

  // Unsubscribe page (linked from marketing emails)
  {
    path: 'unsubscribe',
    loadComponent: () =>
      import('./pages/unsubscribe/unsubscribe.component').then(m => m.UnsubscribeComponent),
    title: 'Unsubscribe - AI Profile Photo Maker',
    data: { hideNavigation: true },
  },

  // 404 and Wildcard - Must be last
  {
    path: '404',
    loadComponent: () => import('./pages/landing/landing.component').then(m => m.LandingComponent),
    title: 'Page Not Found - AI Profile Photo Maker',
    data: { showNotFound: true },
  },
  {
    path: '**',
    redirectTo: '/404',
  },
];

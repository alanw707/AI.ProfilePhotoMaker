import { inject } from '@angular/core';
import { Router, Routes } from '@angular/router';
import { LoginComponent } from './auth/login/login.component';
import { RegisterComponent } from './auth/register/register.component';
import { guestGuard } from './guards/guest.guard';
import { AppGuard } from './guards/app.guard';
import { seoPages } from './pages/marketing/seo-pages.data';

export const routes: Routes = [
  // Home/Landing Page - Main entry point
  {
    path: '',
    loadComponent: () => import('./pages/landing/landing.component').then(m => m.LandingComponent),
    pathMatch: 'full',
    title: 'AI Profile Photo Maker - Professional Headshots with AI',
    data: {
      meta: {
        description:
          'Transform your casual photos into professional headshots with AI. Perfect for LinkedIn, dating apps, and social media.',
        keywords: 'AI profile photo, professional headshot, LinkedIn photo, AI photo enhancement',
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
        redirectTo: 'dashboard',
        pathMatch: 'full',
      },
      {
        path: 'dashboard',
        loadComponent: () =>
          import('./dashboard/dashboard.component').then(m => m.DashboardComponent),
        title: 'Dashboard - AI Profile Photo Maker',
        data: {
          breadcrumb: 'Dashboard',
          hideNavigation: false,
        },
      },
      {
        path: 'enhance',
        loadComponent: () =>
          import('./components/photo-enhancement/photo-enhancement.component').then(
            m => m.PhotoEnhancementComponent
          ),
        title: 'Enhance Photos - AI Profile Photo Maker',
        data: {
          breadcrumb: 'Enhance Photos',
          hideNavigation: false,
        },
      },
      {
        path: 'gallery',
        loadComponent: () =>
          import('./pages/gallery/gallery.component').then(m => m.GalleryComponent),
        title: 'Photo Gallery - AI Profile Photo Maker',
        data: {
          breadcrumb: 'Gallery',
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
        return router.navigate(['/app/dashboard'], { queryParams: params });
      },
    ],
    pathMatch: 'full',
    loadComponent: () => import('./dashboard/dashboard.component').then(m => m.DashboardComponent),
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
    loadComponent: () =>
      import('./pages/marketing/seo-page/seo-page.component').then(m => m.SeoPageComponent),
    title: seoPages['reviews'].title,
    data: {
      seoPage: seoPages['reviews'],
    },
  },
  {
    path: 'free-headshot-enhancer',
    loadComponent: () =>
      import('./pages/marketing/seo-page/seo-page.component').then(m => m.SeoPageComponent),
    title: seoPages['free-headshot-enhancer'].title,
    data: {
      seoPage: seoPages['free-headshot-enhancer'],
    },
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

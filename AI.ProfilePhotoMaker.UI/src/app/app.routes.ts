import { inject } from '@angular/core';
import { Router, Routes } from '@angular/router';
import { LoginComponent } from './auth/login/login.component';
import { RegisterComponent } from './auth/register/register.component';
import { guestGuard } from './guards/guest.guard';
import { AppGuard } from './guards/app.guard';

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
        path: 'cookies',
        loadComponent: () =>
          import('./pages/privacy/privacy.component').then(m => m.PrivacyComponent),
        title: 'Cookie Policy - AI Profile Photo Maker',
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
    loadComponent: () => import('./pages/landing/landing.component').then(m => m.LandingComponent),
    title: 'Features - AI Profile Photo Maker',
    data: {
      scrollTo: 'features',
      meta: {
        description:
          'Discover powerful AI features that transform your photos into professional headshots instantly.',
        keywords: 'AI photo features, professional headshot generator, photo enhancement AI',
      },
    },
  },
  {
    path: 'examples',
    loadComponent: () => import('./pages/landing/landing.component').then(m => m.LandingComponent),
    title: 'Examples - AI Profile Photo Maker',
    data: {
      scrollTo: 'examples',
      meta: {
        description: 'See amazing before and after examples of AI-transformed profile photos.',
        keywords: 'AI photo examples, before after photos, professional headshot gallery',
      },
    },
  },

  // Help & Support
  {
    path: 'help',
    children: [
      {
        path: '',
        loadComponent: () =>
          import('./pages/landing/landing.component').then(m => m.LandingComponent),
        title: 'Help & FAQ - AI Profile Photo Maker',
        data: {
          scrollTo: 'faq',
          meta: {
            description: 'Get answers to frequently asked questions about AI Profile Photo Maker.',
            keywords: 'AI photo help, FAQ, support, how to use',
          },
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

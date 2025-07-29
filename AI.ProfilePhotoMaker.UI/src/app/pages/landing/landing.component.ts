import { Component, ElementRef, HostListener, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { Meta, Title } from '@angular/platform-browser';
import { animate, state, style, transition, trigger } from '@angular/animations';
import { Observable, Subscription } from 'rxjs';
import { NavigationService } from '../../services/navigation.service';
import { ThemeService } from '../../services/theme.service';
import { CreditService, CreditPackage } from '../../services/credit.service';

interface Plan {
  name: string;
  price: string;
  originalPrice?: string;
  features: string[];
  recommended?: boolean;
  creditCount: string;
}

interface Testimonial {
  name: string;
  role: string;
  content: string;
  avatar: string;
  rating: number;
}

interface FAQ {
  question: string;
  answer: string;
  open?: boolean;
}

@Component({
  selector: 'app-landing',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './landing.component.html',
  styleUrls: ['./landing.component.sass'],
  animations: [
    trigger('fadeInUp', [
      transition(':enter', [
        style({ opacity: 0, transform: 'translateY(20px)' }),
        animate('0.6s ease-out', style({ opacity: 1, transform: 'translateY(0)' })),
      ]),
    ]),
    trigger('fadeIn', [
      transition(':enter', [
        style({ opacity: 0 }),
        animate('0.6s ease-out', style({ opacity: 1 })),
      ]),
    ]),
  ],
})
export class LandingComponent implements OnInit, OnDestroy {
  isScrolled = false;
  mobileMenuOpen = false;
  currentBeforeAfterIndex = 0;
  currentTestimonialIndex = 0;
  isComparisonDragging = false;
  comparisonPosition = 50;
  newsletterEmail = '';
  showThankYou = false;
  showNotFound = false;

  // Theme-related properties
  currentTheme$!: Observable<string>;
  private themeSubscription: Subscription = new Subscription();

  @ViewChild('comparisonSlider') comparisonSlider!: ElementRef;

  features = [
    {
      icon: '🤖',
      title: 'AI-Powered Enhancement',
      description:
        'Advanced AI technology that transforms your casual photos into professional headshots',
    },
    {
      icon: '🎨',
      title: 'Multiple Style Options',
      description:
        'Choose from 20+ professional styles including LinkedIn, corporate, creative, and more',
    },
    {
      icon: '⚡',
      title: 'Instant Results',
      description: 'Get your enhanced photos in minutes, not hours or days',
    },
    {
      icon: '🔒',
      title: 'Privacy First',
      description: 'Your photos are encrypted and automatically deleted after processing',
    },
    {
      icon: '📱',
      title: 'Works Everywhere',
      description: 'Access from any device - desktop, tablet, or mobile',
    },
    {
      icon: '💎',
      title: 'HD Quality',
      description: 'High-resolution outputs perfect for all professional platforms',
    },
  ];

  plans: Plan[] = [];
  isLoadingPackages = true;

  testimonials: Testimonial[] = [
    {
      name: 'Sarah Johnson',
      role: 'Marketing Director',
      content:
        'The AI transformed my casual selfie into a professional headshot that looks like it was taken in a studio. Amazing!',
      avatar: '👩‍💼',
      rating: 5,
    },
    {
      name: 'Michael Chen',
      role: 'Software Engineer',
      content:
        'Finally updated my LinkedIn photo after years. The quality is incredible and it only took 2 minutes!',
      avatar: '👨‍💻',
      rating: 5,
    },
    {
      name: 'Emily Rodriguez',
      role: 'Freelance Designer',
      content:
        'I use different styles for different platforms. The variety and quality are unmatched. Worth every penny!',
      avatar: '👩‍🎨',
      rating: 5,
    },
  ];

  faqs: FAQ[] = [
    {
      question: 'How does the AI enhancement work?',
      answer:
        'Our advanced AI analyzes your photo, enhances facial features, improves lighting, and applies professional styling while maintaining your natural appearance. The process typically takes 1-2 minutes per photo.',
    },
    {
      question: 'What photo formats are supported?',
      answer:
        'We support all major image formats including JPG, PNG, WEBP, and HEIF. Photos should be at least 512x512 pixels for best results.',
    },
    {
      question: 'Are my photos safe and private?',
      answer:
        'Absolutely! All photos are encrypted during upload and processing. We automatically delete your original photos after 24 hours and never share your data with third parties.',
    },
    {
      question: 'Can I use the photos commercially?',
      answer:
        'Yes! You have full commercial rights to all enhanced photos. Use them for LinkedIn, resumes, websites, business cards, or any other purpose.',
    },
    {
      question: "What if I'm not satisfied with the results?",
      answer:
        "We offer a 100% satisfaction guarantee. If you're not happy with your enhanced photos, contact us within 7 days for a full refund.",
    },
    {
      question: 'Do you offer team or enterprise plans?',
      answer:
        'Yes! We have custom plans for teams and enterprises with bulk pricing, API access, and dedicated support. Contact us for more information.',
    },
  ];

  beforeAfterExamples = [
    {
      before: '/assets/examples/before-1.jpg',
      after: '/assets/examples/after-1.jpg',
      style: 'Professional LinkedIn',
      description: 'Perfect for professional networking',
    },
    {
      before: '/assets/examples/before-2.jpg',
      after: '/assets/examples/after-2.jpg',
      style: 'Corporate Executive',
      description: 'Ideal for C-suite and leadership roles',
    },
    {
      before: '/assets/examples/before-3.jpg',
      after: '/assets/examples/after-3.jpg',
      style: 'Creative Professional',
      description: 'Great for designers and artists',
    },
  ];

  stats = [
    { value: '2,847+', label: 'Happy Customers' },
    { value: '4.9/5', label: 'Average Rating' },
    { value: '< 2min', label: 'Processing Time' },
    { value: '100%', label: 'Privacy Guaranteed' },
  ];

  constructor(
    private meta: Meta,
    private title: Title,
    public router: Router,
    private route: ActivatedRoute,
    public navigation: NavigationService,
    public themeService: ThemeService,
    private creditService: CreditService
  ) {
    this.currentTheme$ = this.themeService.theme$;
  }

  ngOnInit(): void {
    this.setupSEO();
    this.loadPackagesFromDatabase();
    this.startBeforeAfterRotation();
    this.startTestimonialRotation();
    this.observeElements();
    this.handleRouteData();
  }

  ngOnDestroy(): void {
    this.themeSubscription.unsubscribe();
  }

  toggleTheme(): void {
    this.themeService.toggleTheme();
  }

  loadPackagesFromDatabase(): void {
    this.isLoadingPackages = true;
    this.creditService.getCreditPackages().subscribe({
      next: response => {
        if (response && response.success && response.data) {
          // Map database packages to landing page format
          this.plans = response.data.map((pkg: CreditPackage) => ({
            name: pkg.name.replace(' Pack', ''), // Remove "Pack" suffix
            price: `$${Math.floor(pkg.price)}`, // Format price
            originalPrice: pkg.bonusCredits > 0 ? `$${Math.floor(pkg.price + 10)}` : undefined,
            features: this.getPackageFeatures(pkg),
            recommended: pkg.name === 'Professional Pack',
            creditCount: `${pkg.totalCredits} credits`,
          }));
        }
      },
      error: error => {
        console.error('Failed to load packages:', error);
        // Fallback to default packages if loading fails
        this.setDefaultPackages();
      },
      complete: () => {
        this.isLoadingPackages = false;
      },
    });
  }

  private getPackageFeatures(pkg: CreditPackage): string[] {
    if (pkg.name === 'Starter Pack') {
      return [
        `${pkg.totalCredits} AI-enhanced photos`,
        'Basic styles',
        'Standard resolution',
        'Email support',
      ];
    } else if (pkg.name === 'Professional Pack') {
      return [
        `${pkg.totalCredits} AI-enhanced photos`,
        'All premium styles',
        'HD resolution',
        'Priority processing',
        'Download all formats',
      ];
    } else if (pkg.name === 'Studio Pack') {
      return [
        `${pkg.totalCredits} AI-enhanced photos`,
        'All premium styles',
        'HD+ resolution',
        'Priority support',
        'Advanced editing',
        'Commercial license',
      ];
    }
    return [];
  }

  private setDefaultPackages(): void {
    this.plans = [
      {
        name: 'Starter',
        price: '$9',
        features: ['50 AI-enhanced photos', 'Basic styles', 'Standard resolution', 'Email support'],
        creditCount: '50 credits',
      },
      {
        name: 'Professional',
        price: '$19',
        originalPrice: '$29',
        features: [
          '150 AI-enhanced photos',
          'All premium styles',
          'HD resolution',
          'Priority processing',
          'Download all formats',
        ],
        recommended: true,
        creditCount: '150 credits',
      },
      {
        name: 'Studio',
        price: '$39',
        features: [
          '400 AI-enhanced photos',
          'All premium styles',
          'HD+ resolution',
          'Priority support',
          'Advanced editing',
          'Commercial license',
        ],
        creditCount: '400 credits',
      },
    ];
  }

  setupSEO(): void {
    // Set page title
    this.title.setTitle(
      'AI Profile Photo Maker - Transform Your Photos into Professional Headshots | Instant AI Enhancement'
    );

    // Meta tags
    this.meta.updateTag({
      name: 'description',
      content:
        'Create stunning professional profile photos with AI in seconds. Perfect for LinkedIn, dating apps, resumes, and social media. Transform casual selfies into polished headshots. Try free - no credit card required.',
    });
    this.meta.updateTag({
      name: 'keywords',
      content:
        'AI profile photo maker, professional headshot generator, LinkedIn photo AI, AI photo enhancement, profile picture creator, headshot AI tool, professional photo maker, AI portrait generator, business photo creator, social media profile photo',
    });
    this.meta.updateTag({ name: 'author', content: 'AI Profile Photo Maker' });
    this.meta.updateTag({ name: 'robots', content: 'index, follow' });
    this.meta.updateTag({ name: 'viewport', content: 'width=device-width, initial-scale=1' });

    // Open Graph tags
    this.meta.updateTag({
      property: 'og:title',
      content: 'AI Profile Photo Maker - Professional Headshots in Minutes',
    });
    this.meta.updateTag({
      property: 'og:description',
      content:
        'Transform your casual photos into professional headshots with AI. Perfect for LinkedIn, resumes, and social media. Try free today!',
    });
    this.meta.updateTag({ property: 'og:type', content: 'website' });
    this.meta.updateTag({ property: 'og:url', content: 'https://aiprofilephotomaker.com' });
    this.meta.updateTag({
      property: 'og:image',
      content: 'https://aiprofilephotomaker.com/assets/og-image.jpg',
    });
    this.meta.updateTag({ property: 'og:image:width', content: '1200' });
    this.meta.updateTag({ property: 'og:image:height', content: '630' });
    this.meta.updateTag({ property: 'og:site_name', content: 'AI Profile Photo Maker' });

    // Twitter Card tags
    this.meta.updateTag({ name: 'twitter:card', content: 'summary_large_image' });
    this.meta.updateTag({
      name: 'twitter:title',
      content: 'AI Profile Photo Maker - Create Professional Headshots with AI',
    });
    this.meta.updateTag({
      name: 'twitter:description',
      content:
        'Transform casual photos into professional headshots in seconds. Perfect for LinkedIn, dating apps & social media.',
    });
    this.meta.updateTag({
      name: 'twitter:image',
      content: 'https://aiprofilephotomaker.com/assets/twitter-card.jpg',
    });
    this.meta.updateTag({ name: 'twitter:creator', content: '@aiprofilephoto' });

    // Additional SEO tags
    this.meta.updateTag({ name: 'theme-color', content: '#4F46E5' });
    this.meta.updateTag({ name: 'apple-mobile-web-app-capable', content: 'yes' });
    this.meta.updateTag({ name: 'apple-mobile-web-app-status-bar-style', content: 'default' });

    // Structured data for SEO
    const structuredData = {
      '@context': 'https://schema.org',
      '@type': 'SoftwareApplication',
      name: 'AI Profile Photo Maker',
      description:
        'Transform casual photos into professional headshots with AI technology. Create stunning LinkedIn photos, dating app profiles, and social media pictures in seconds.',
      applicationCategory: 'PhotographyApplication',
      operatingSystem: 'Web',
      url: 'https://aiprofilephotomaker.com',
      image: 'https://aiprofilephotomaker.com/assets/Logo.PNG',
      screenshot: 'https://aiprofilephotomaker.com/assets/screenshot.jpg',
      offers: {
        '@type': 'AggregateOffer',
        lowPrice: '9.99',
        highPrice: '79.99',
        priceCurrency: 'USD',
        offerCount: '3',
      },
      aggregateRating: {
        '@type': 'AggregateRating',
        ratingValue: '4.9',
        reviewCount: '2847',
        bestRating: '5',
        worstRating: '1',
      },
      creator: {
        '@type': 'Organization',
        name: 'AI Profile Photo Maker',
        url: 'https://aiprofilephotomaker.com',
      },
      datePublished: '2024-01-01',
      featureList: [
        'AI-powered photo enhancement',
        '20+ professional style options',
        'Instant processing',
        'Privacy-first approach',
        'HD quality output',
        'Cross-platform compatibility',
      ],
    };

    const script = document.createElement('script');
    script.type = 'application/ld+json';
    script.text = JSON.stringify(structuredData);
    document.head.appendChild(script);

    // Add FAQ structured data
    const faqData = {
      '@context': 'https://schema.org',
      '@type': 'FAQPage',
      mainEntity: this.faqs.map(faq => ({
        '@type': 'Question',
        name: faq.question,
        acceptedAnswer: {
          '@type': 'Answer',
          text: faq.answer,
        },
      })),
    };

    const faqScript = document.createElement('script');
    faqScript.type = 'application/ld+json';
    faqScript.text = JSON.stringify(faqData);
    document.head.appendChild(faqScript);
  }

  @HostListener('window:scroll', [])
  onWindowScroll(): void {
    this.isScrolled = window.scrollY > 20;
  }

  toggleMobileMenu(): void {
    this.mobileMenuOpen = !this.mobileMenuOpen;
  }

  toggleFAQ(index: number): void {
    this.faqs[index].open = !this.faqs[index].open;
  }

  scrollToSection(sectionId: string): void {
    this.navigation.scrollToSection(sectionId);
    this.mobileMenuOpen = false;
  }

  getStarted(): void {
    this.navigation.goToRegister();
  }

  startBeforeAfterRotation(): void {
    setInterval(() => {
      this.currentBeforeAfterIndex =
        (this.currentBeforeAfterIndex + 1) % this.beforeAfterExamples.length;
    }, 4000);
  }

  startTestimonialRotation(): void {
    setInterval(() => {
      this.currentTestimonialIndex = (this.currentTestimonialIndex + 1) % this.testimonials.length;
    }, 5000);
  }

  observeElements(): void {
    const observer = new IntersectionObserver(
      entries => {
        entries.forEach(entry => {
          if (entry.isIntersecting) {
            entry.target.classList.add('animate-in');
          }
        });
      },
      { threshold: 0.1 }
    );

    // Observe all animatable elements
    setTimeout(() => {
      const elements = document.querySelectorAll('.animate-on-scroll');
      elements.forEach(el => observer.observe(el));
    }, 100);
  }

  startComparison(event: MouseEvent | TouchEvent): void {
    this.isComparisonDragging = true;
    this.updateComparisonPosition(event);
  }

  moveComparison(event: MouseEvent | TouchEvent): void {
    if (!this.isComparisonDragging) {
      return;
    }
    this.updateComparisonPosition(event);
  }

  endComparison(): void {
    this.isComparisonDragging = false;
  }

  updateComparisonPosition(event: MouseEvent | TouchEvent): void {
    if (!this.comparisonSlider) {
      return;
    }

    const rect = this.comparisonSlider.nativeElement.getBoundingClientRect();
    const x = event instanceof MouseEvent ? event.clientX : event.touches[0].clientX;
    const position = ((x - rect.left) / rect.width) * 100;

    this.comparisonPosition = Math.max(0, Math.min(100, position));
  }

  subscribeNewsletter(): void {
    if (this.newsletterEmail && this.newsletterEmail.includes('@')) {
      // Here you would normally send the email to your backend
      this.showThankYou = true;
      this.newsletterEmail = '';

      setTimeout(() => {
        this.showThankYou = false;
      }, 5000);
    }
  }

  handleRouteData(): void {
    // Handle route data for scrolling and special views
    this.route.data.subscribe(data => {
      if (data['scrollTo']) {
        setTimeout(() => {
          this.scrollToSection(data['scrollTo']);
        }, 500);
      }

      if (data['showNotFound']) {
        this.showNotFound = true;
      }

      // Update meta tags if provided
      if (data['meta']) {
        if (data['meta']['description']) {
          this.meta.updateTag({ name: 'description', content: data['meta']['description'] });
        }
        if (data['meta']['keywords']) {
          this.meta.updateTag({ name: 'keywords', content: data['meta']['keywords'] });
        }
      }
    });
  }

  navigateToLogin(): void {
    this.navigation.goToLogin();
  }

  navigateToDashboard(): void {
    this.navigation.goToDashboard();
  }

  navigateToPricing(): void {
    this.navigation.goToPricing();
  }
}

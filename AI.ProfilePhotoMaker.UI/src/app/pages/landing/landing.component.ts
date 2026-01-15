import {
  ChangeDetectorRef,
  Component,
  ElementRef,
  OnDestroy,
  OnInit,
  ViewChild,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { Meta, Title } from '@angular/platform-browser';
import { animate, style, transition, trigger } from '@angular/animations';
import { Observable, Subscription } from 'rxjs';
import { NavigationService } from '../../services/navigation.service';
import { ThemeService } from '../../services/theme.service';
import { CreditPackage, CreditService } from '../../services/credit.service';
import { Style, StyleService } from '../../services/style.service';
import { ConfigService } from '../../services/config.service';
import { StylePreviewService } from '../../services/style-preview.service';
import { AuthService } from '../../services/auth.service';
import { CookieConsentService } from '../../services/cookie-consent.service';
import { MarketingHeaderComponent } from '../../shared/marketing-header/marketing-header.component';
import { MarketingFooterComponent } from '../../shared/marketing-footer/marketing-footer.component';

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
  style: string;
  imageUrl: string;
}

interface FAQ {
  question: string;
  answer: string;
  open?: boolean;
}

interface StyledPhoto {
  id: number;
  imageUrl: string;
  style: string;
  category: string;
  description: string;
}

@Component({
  selector: 'app-landing',
  standalone: true,
  imports: [CommonModule, RouterModule, MarketingHeaderComponent, MarketingFooterComponent],
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
  isAuthenticated = false;
  currentBeforeAfterIndex = 0;
  currentTestimonialIndex = 0;
  isComparisonDragging = false;
  comparisonPosition = 50;
  newsletterEmail = '';
  showThankYou = false;
  showNotFound = false;
  private readonly structuredDataId = 'landing-structured-data';
  private readonly faqStructuredDataId = 'landing-faq-structured-data';

  // Theme-related properties
  currentTheme$!: Observable<string>;
  private themeSubscription: Subscription = new Subscription();

  // Styled photos showcase - removed carousel, now using grid

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

  testimonials: Testimonial[] = [];

  faqs: FAQ[] = [
    {
      question: 'How does the AI enhancement work?',
      answer:
        'Our advanced AI analyzes your photo, enhances facial features, improves lighting, and applies professional styling while maintaining your natural appearance. The process typically takes 1-2 minutes per photo.',
    },
    {
      question: 'What photo formats are supported?',
      answer:
        'We support JPG/JPEG, PNG, and WEBP. Photos should be at least 512x512 pixels for best results.',
    },
    {
      question: 'Are my photos safe and private?',
      answer:
        'Absolutely! All photos are encrypted during upload and processing. We typically delete your original photos within 30 days and never share your data with third parties.',
    },
    {
      question: 'Can I use the photos commercially?',
      answer:
        'Yes! You have full commercial rights to all enhanced photos. Use them for LinkedIn, resumes, websites, business cards, or any other purpose.',
    },
    {
      question: 'How do credit packages work?',
      answer:
        'Credit packages are one-time purchases that unlock headshot style generations and model training. Credits never expire, so you can use them whenever you are ready.',
    },
    {
      question: "What if I'm not satisfied with the results?",
      answer:
        "We offer a 14-day satisfaction guarantee. If you're not happy with your results, contact us within 14 days for a refund.",
    },
    {
      question: 'Do you offer team or enterprise plans?',
      answer:
        'Yes! We have custom plans for teams and enterprises with bulk pricing, API access, and dedicated support. Contact us for more information.',
    },
  ];

  styledPhotos: StyledPhoto[] = [];
  availableStyles: Style[] = [];
  isLoadingStyles = true;
  stylesLoadError = false;
  private styledGenerationCost = 0;
  private modelTrainingCost = 0;

  stats = [
    { value: '5', label: 'Weekly top-up target' },
    { value: 'Minutes', label: 'Typical results' },
    { value: '20+', label: 'Styles' },
    { value: 'Privacy-first', label: 'Handling' },
  ];

  constructor(
    private _meta: Meta,
    private _title: Title,
    public router: Router,
    private _route: ActivatedRoute,
    public navigation: NavigationService,
    public themeService: ThemeService,
    private _authService: AuthService,
    private _creditService: CreditService,
    private _styleService: StyleService,
    private _config: ConfigService,
    private _stylePreviewService: StylePreviewService,
    private _cookieConsentService: CookieConsentService,
    private _cdr: ChangeDetectorRef
  ) {
    this.currentTheme$ = this.themeService.theme$;
    this.setFallbackCreditCosts();
  }

  ngOnInit(): void {
    this.isAuthenticated = this._authService.isAuthenticated();
    this.themeSubscription.add(
      this._authService.isAuthenticated$.subscribe(isAuth => {
        this.isAuthenticated = isAuth;
      })
    );
    this.setupSEO();
    this.initializeTestimonials();
    this.loadPackagesFromDatabase();
    this.loadAvailableStyles();
    this.startTestimonialRotation();
    this.observeElements();
    this.handleRouteData();
  }

  ngOnDestroy(): void {
    this.themeSubscription.unsubscribe();

    const structuredDataScript = document.getElementById(this.structuredDataId);
    if (structuredDataScript) {
      structuredDataScript.remove();
    }

    const faqDataScript = document.getElementById(this.faqStructuredDataId);
    if (faqDataScript) {
      faqDataScript.remove();
    }
  }

  toggleTheme(): void {
    this.themeService.toggleTheme();
  }

  openCookiePreferences(): void {
    this._cookieConsentService.requestPreferencesOpen();
  }

  loadPackagesFromDatabase(): void {
    this.isLoadingPackages = true;
    this._creditService.getCreditCosts().subscribe({
      next: response => {
        if (response?.success && response.data) {
          const styledCost = response.data.styledGeneration.cost;
          const trainingCost = response.data.modelTraining.cost;
          this.styledGenerationCost = styledCost > 0 ? styledCost : this.styledGenerationCost;
          this.modelTrainingCost = trainingCost > 0 ? trainingCost : this.modelTrainingCost;
        } else {
          this.setFallbackCreditCosts();
        }
        this.fetchPackagesFromDatabase();
      },
      error: error => {
        console.error('Failed to load credit costs:', error);
        this.setFallbackCreditCosts();
        this.fetchPackagesFromDatabase();
      },
    });
  }

  private fetchPackagesFromDatabase(): void {
    this._creditService.getCreditPackages().subscribe({
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
    return this.buildPackageFeatures(pkg.name, pkg.totalCredits);
  }

  private buildPackageFeatures(packageName: string, totalCredits: number): string[] {
    const normalizedName = packageName.toLowerCase();
    const styledPhotoFeature = this.getStyledPhotoFeature(totalCredits);
    const trainingFeature = this.getTrainingFeature(totalCredits);

    if (normalizedName.includes('starter')) {
      return [
        styledPhotoFeature,
        trainingFeature,
        'Basic styles',
        'Standard resolution',
        'Email support',
      ];
    }
    if (normalizedName.includes('professional')) {
      return [
        styledPhotoFeature,
        trainingFeature,
        'All premium styles',
        'HD resolution',
        'Priority processing',
        'Download all formats',
      ];
    }
    if (normalizedName.includes('studio')) {
      return [
        styledPhotoFeature,
        trainingFeature,
        'All premium styles',
        'HD+ resolution',
        'Priority support',
        'Advanced editing',
        'Commercial license',
      ];
    }
    return [styledPhotoFeature, trainingFeature];
  }

  private getStyledPhotoFeature(totalCredits: number): string {
    const count = this.getStyledGenerationCount(totalCredits);
    const label = count === 1 ? 'headshot style photo' : 'headshot style photos';
    return `~${count} ${label}`;
  }

  private getTrainingFeature(totalCredits: number): string {
    const trainingCost =
      this.modelTrainingCost || this._creditService.getCreditCostSync('model_training');
    if (totalCredits >= trainingCost) {
      return 'Model training included';
    }
    return `Model training available with ${trainingCost}+ credits`;
  }

  private getStyledGenerationCount(totalCredits: number): number {
    const styledCost =
      this.styledGenerationCost || this._creditService.getCreditCostSync('styled_generation');
    if (styledCost <= 0) {
      return 0;
    }
    return Math.floor(totalCredits / styledCost);
  }

  private setDefaultPackages(): void {
    this.plans = [
      {
        name: 'Starter',
        price: '$9',
        features: this.buildPackageFeatures('Starter', 50),
        creditCount: '50 credits',
      },
      {
        name: 'Professional',
        price: '$19',
        originalPrice: '$29',
        features: this.buildPackageFeatures('Professional', 150),
        recommended: true,
        creditCount: '150 credits',
      },
      {
        name: 'Studio',
        price: '$39',
        features: this.buildPackageFeatures('Studio', 400),
        creditCount: '400 credits',
      },
    ];
  }

  private setFallbackCreditCosts(): void {
    this.styledGenerationCost = this._creditService.getCreditCostSync('styled_generation');
    this.modelTrainingCost = this._creditService.getCreditCostSync('model_training');
  }

  setupSEO(): void {
    this.setCanonicalUrl('https://aiprofilephotomaker.com/');
    // Set page title
    this._title.setTitle(
      'AI Profile Photo Maker - Transform Your Photos into Professional Headshots | Instant AI Enhancement'
    );

    // Meta tags
    this._meta.updateTag({
      name: 'description',
      content:
        'Create LinkedIn-ready profile photos with AI in minutes. A single credit balance powers enhancement, training, and generation, with weekly top-ups to 5 when below.',
    });
    this._meta.updateTag({
      name: 'keywords',
      content:
        'AI profile photo maker, professional headshot generator, LinkedIn photo AI, AI photo enhancement, profile picture creator, headshot AI tool, professional photo maker, AI portrait generator, business photo creator, social media profile photo',
    });
    this._meta.updateTag({ name: 'author', content: 'AI Profile Photo Maker' });
    this._meta.updateTag({ name: 'robots', content: 'index, follow' });
    this._meta.updateTag({ name: 'viewport', content: 'width=device-width, initial-scale=1' });

    // Open Graph tags
    this._meta.updateTag({
      property: 'og:title',
      content: 'AI Profile Photo Maker - Professional Headshots in Minutes',
    });
    this._meta.updateTag({
      property: 'og:description',
      content:
        'Transform your casual photos into professional profile photos with AI. Use one credit balance for enhancement, training, and generation, with weekly top-ups to 5 when below.',
    });
    this._meta.updateTag({ property: 'og:type', content: 'website' });
    this._meta.updateTag({ property: 'og:url', content: 'https://aiprofilephotomaker.com/' });
    this._meta.updateTag({
      property: 'og:image',
      content: 'https://aiprofilephotomaker.com/assets/og-image.png?v=2',
    });
    this._meta.updateTag({ property: 'og:image:width', content: '945' });
    this._meta.updateTag({ property: 'og:image:height', content: '631' });
    this._meta.updateTag({ property: 'og:site_name', content: 'AI Profile Photo Maker' });

    // Twitter Card tags
    this._meta.updateTag({ name: 'twitter:card', content: 'summary_large_image' });
    this._meta.updateTag({
      name: 'twitter:title',
      content: 'AI Profile Photo Maker - Create Professional Headshots with AI',
    });
    this._meta.updateTag({
      name: 'twitter:description',
      content:
        'Create LinkedIn-ready profile photos in minutes. One credit balance covers enhancement, training, and generation, with weekly top-ups to 5 when below.',
    });
    this._meta.updateTag({
      name: 'twitter:image',
      content: 'https://aiprofilephotomaker.com/assets/twitter-card.png',
    });
    this._meta.updateTag({ name: 'twitter:creator', content: '@aiprofilephoto' });

    // Additional SEO tags
    this._meta.updateTag({ name: 'theme-color', content: '#4F46E5' });
    this._meta.updateTag({ name: 'apple-mobile-web-app-capable', content: 'yes' });
    this._meta.updateTag({ name: 'apple-mobile-web-app-status-bar-style', content: 'default' });

    // Structured data for SEO
    const structuredData = {
      '@context': 'https://schema.org',
      '@type': 'SoftwareApplication',
      name: 'AI Profile Photo Maker',
      description:
        'Transform casual photos into professional profile photos with AI technology. Create stunning LinkedIn photos, resumes, and social media pictures in minutes.',
      applicationCategory: 'PhotographyApplication',
      operatingSystem: 'Web',
      url: 'https://aiprofilephotomaker.com/',
      image: 'https://aiprofilephotomaker.com/assets/Logo.PNG',
      screenshot: 'https://aiprofilephotomaker.com/assets/og-image.png?v=2',
      offers: {
        '@type': 'AggregateOffer',
        lowPrice: '9.99',
        highPrice: '39.99',
        priceCurrency: 'USD',
        offerCount: '3',
      },
      creator: {
        '@type': 'Organization',
        name: 'AI Profile Photo Maker',
        url: 'https://aiprofilephotomaker.com/',
        logo: 'https://aiprofilephotomaker.com/Logo.PNG',
      },
      datePublished: '2024-01-01',
      featureList: [
        'AI-powered photo enhancement',
        '20+ professional style options',
        'Minutes typical results',
        'Privacy-first approach',
        'HD quality output',
        'Cross-platform compatibility',
      ],
    };

    const existingStructuredData = document.getElementById(this.structuredDataId);
    if (existingStructuredData) {
      existingStructuredData.remove();
    }

    const script = document.createElement('script');
    script.id = this.structuredDataId;
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

    const existingFaqData = document.getElementById(this.faqStructuredDataId);
    if (existingFaqData) {
      existingFaqData.remove();
    }

    const faqScript = document.createElement('script');
    faqScript.id = this.faqStructuredDataId;
    faqScript.type = 'application/ld+json';
    faqScript.text = JSON.stringify(faqData);
    document.head.appendChild(faqScript);
  }

  toggleFAQ(index: number): void {
    this.faqs[index].open = !this.faqs[index].open;
  }

  private setCanonicalUrl(url: string): void {
    let canonical = document.querySelector("link[rel='canonical']") as HTMLLinkElement | null;
    if (!canonical) {
      canonical = document.createElement('link');
      canonical.setAttribute('rel', 'canonical');
      document.head.appendChild(canonical);
    }
    canonical.setAttribute('href', url);
  }

  private isLandingSection(
    sectionId: string
  ): sectionId is 'features' | 'examples' | 'pricing' | 'testimonials' | 'faq' {
    return ['features', 'examples', 'pricing', 'testimonials', 'faq'].includes(sectionId);
  }

  scrollToSection(sectionId: string): void {
    const currentRoute = this.navigation.getCurrentRoute().split('#')[0].split('?')[0];
    if (currentRoute && currentRoute !== '/') {
      if (this.isLandingSection(sectionId)) {
        void this.navigation.navigateToSection(sectionId);
      } else {
        void this.navigation.navigateTo('/', { fragment: sectionId });
      }
      return;
    }

    void this.router.navigate([], { fragment: sectionId, relativeTo: this._route });
    this.navigation.scrollToSection(sectionId);
  }

  getStarted(): void {
    if (this.isAuthenticated) {
      void this.navigation.goToEnhance();
      return;
    }
    void this.navigation.navigateTo('/auth/login', { queryParams: { returnUrl: '/app/enhance' } });
  }

  loadAvailableStyles(): void {
    this.isLoadingStyles = true;
    this.stylesLoadError = false;
    const finishLoading = () => {
      this.isLoadingStyles = false;
      this._cdr.detectChanges();
    };

    this._styleService.getActiveStyles().subscribe({
      next: response => {
        if (response.success && response.data && response.data.length > 0) {
          console.log(`Successfully loaded ${response.data.length} styles from database`);
          this.availableStyles = response.data;
          this.createStyledPhotosFromStyles();
          this.stylesLoadError = false;
        } else {
          console.warn('No styles found in database, using fallback data');
          this.createFallbackStyledPhotos();
          this.stylesLoadError = true;
        }
        finishLoading();
      },
      error: error => {
        console.error('Error loading styles from database:', error);
        console.log('Falling back to predefined styles');
        this.createFallbackStyledPhotos();
        this.stylesLoadError = true;
        finishLoading();
      },
    });
  }

  createStyledPhotosFromStyles(): void {
    // Create styled photos array from actual styles
    this.styledPhotos = this.availableStyles.slice(0, 20).map(style => ({
      id: style.id,
      imageUrl: this._stylePreviewService.getCachedUrl(style.name),
      style: style.name,
      category: this.getCategoryFromStyleName(style.name),
      description: style.description,
    }));

    // Subscribe to preview URL updates
    this._stylePreviewService.getAllPreviewUrls().subscribe(urlMap => {
      // Update the image URLs when they become available
      this.styledPhotos.forEach(photo => {
        const updatedUrl = urlMap.get(photo.style);
        if (updatedUrl && updatedUrl !== photo.imageUrl) {
          photo.imageUrl = updatedUrl;
        }
      });
    });
  }

  createFallbackStyledPhotos(): void {
    // Fallback data in case styles can't be loaded
    const fallbackStyles = [
      {
        name: 'Professional LinkedIn',
        description: 'Corporate professional headshot',
        category: 'Business',
      },
      {
        name: 'Creative Professional',
        description: 'Artistic and modern look',
        category: 'Creative',
      },
      {
        name: 'Corporate Executive',
        description: 'C-suite leadership presence',
        category: 'Executive',
      },
      {
        name: 'Casual Professional',
        description: 'Approachable yet professional',
        category: 'Relaxed',
      },
      {
        name: 'Classic Headshot',
        description: 'Timeless professional look',
        category: 'Traditional',
      },
      {
        name: 'Modern Professional',
        description: 'Cutting-edge style',
        category: 'Contemporary',
      },
      {
        name: 'Elegant Portrait',
        description: 'Refined and polished',
        category: 'Sophisticated',
      },
      {
        name: 'Friendly Professional',
        description: 'Warm and welcoming',
        category: 'Approachable',
      },
      {
        name: 'Confident Leader',
        description: 'Strong leadership presence',
        category: 'Leadership',
      },
      {
        name: 'Artistic Expression',
        description: 'Creative industry focused',
        category: 'Creative',
      },
      {
        name: 'Business Casual',
        description: 'Perfect for most industries',
        category: 'Versatile',
      },
      {
        name: 'Tech Professional',
        description: 'Tech industry optimized',
        category: 'Technology',
      },
      {
        name: 'Senior Executive',
        description: 'High-level executive presence',
        category: 'Leadership',
      },
      {
        name: 'Professional Consultant',
        description: 'Expert and trustworthy',
        category: 'Advisory',
      },
      {
        name: 'Entrepreneur',
        description: 'Visionary and forward-thinking',
        category: 'Innovation',
      },
      {
        name: 'Academic Professional',
        description: 'Scholarly and approachable',
        category: 'Education',
      },
      {
        name: 'Sales Professional',
        description: 'Trustworthy and engaging',
        category: 'Sales',
      },
      {
        name: 'Marketing Expert',
        description: 'Creative and strategic',
        category: 'Marketing',
      },
      {
        name: 'Finance Professional',
        description: 'Analytical and precise',
        category: 'Finance',
      },
      {
        name: 'Healthcare Professional',
        description: 'Caring and competent',
        category: 'Healthcare',
      },
    ];

    this.styledPhotos = fallbackStyles.map((style, index) => ({
      id: index + 1,
      imageUrl: this._stylePreviewService.getCachedUrl(style.name),
      style: style.name,
      category: style.category,
      description: style.description,
    }));
  }

  getCategoryFromStyleName(styleName: string): string {
    const name = styleName.toLowerCase();

    // Lifestyle and fun styles
    if (name.includes('beach') || name.includes('vibes')) {
      return 'Lifestyle';
    }
    if (name.includes('fresh') || name.includes('energetic')) {
      return 'Fresh';
    }
    if (name.includes('retro') || name.includes('wave') || name.includes('vintage')) {
      return 'Retro';
    }
    if (name.includes('night') || name.includes('out') || name.includes('evening')) {
      return 'Social';
    }
    if (name.includes('digital') && (name.includes('native') || name.includes('creator'))) {
      return 'Digital';
    }

    // Industry-specific styles
    if (name.includes('medical') || name.includes('healthcare')) {
      return 'Healthcare';
    }
    if (name.includes('academic') || name.includes('scholar')) {
      return 'Academic';
    }
    if (name.includes('fitness') || name.includes('athletic')) {
      return 'Fitness';
    }
    if (name.includes('influencer') || name.includes('social media')) {
      return 'Influencer';
    }
    if (name.includes('entrepreneur') || name.includes('startup')) {
      return 'Business';
    }
    if (name.includes('nomad') || name.includes('remote')) {
      return 'Remote';
    }
    if (name.includes('glamour') || name.includes('glamorous')) {
      return 'Glamour';
    }
    if (name.includes('edgy') || name.includes('urban')) {
      return 'Urban';
    }

    // Professional styles
    if (name.includes('professional') || name.includes('linkedin')) {
      return 'Business';
    }
    if (name.includes('creative') || name.includes('artistic')) {
      return 'Creative';
    }
    if (name.includes('executive') || name.includes('corporate')) {
      return 'Executive';
    }
    if (name.includes('casual')) {
      return 'Relaxed';
    }
    if (name.includes('classic') || name.includes('traditional')) {
      return 'Traditional';
    }
    if (name.includes('modern') || name.includes('contemporary')) {
      return 'Contemporary';
    }
    if (name.includes('elegant') || name.includes('sophisticated')) {
      return 'Sophisticated';
    }
    if (name.includes('friendly') || name.includes('approachable')) {
      return 'Approachable';
    }
    if (name.includes('leader') || name.includes('leadership')) {
      return 'Leadership';
    }
    if (name.includes('tech') || name.includes('technology')) {
      return 'Technology';
    }
    return 'Professional';
  }

  // Style card interaction for new grid layout
  onStyleCardClick(_photo: StyledPhoto, _index: number): void {
    // Trigger get started flow with style context
    this.getStarted();
  }

  formatStyleName(styleName: string): string {
    if (!styleName) {
      return '';
    }
    return styleName
      .split(/[-\s]+/)
      .map(word => word.charAt(0).toUpperCase() + word.slice(1))
      .join(' ');
  }

  startTestimonialRotation(): void {
    setInterval(() => {
      this.currentTestimonialIndex = (this.currentTestimonialIndex + 1) % this.testimonials.length;
    }, 5000);
  }

  private initializeTestimonials(): void {
    const testimonialData: Omit<Testimonial, 'imageUrl'>[] = [
      {
        name: 'Amelia Walsh',
        role: 'Recruitment Consultant (Hobart)',
        content:
          'My LinkedIn profile finally looks polished. The results felt realistic, and I got a set I’m happy to use across applications.',
        style: 'linkedin',
      },
      {
        name: 'Lachlan Reid',
        role: 'Software Engineer (Launceston)',
        content:
          'Super straightforward flow and the turnaround was fast. The enhanced photos looked clean without feeling over-processed.',
        style: 'tech-professional',
      },
      {
        name: 'Sophie Kline',
        role: 'Account Executive (Burnie)',
        content:
          'I needed something professional for client-facing work. The style options helped me find a look that fits my industry.',
        style: 'edgy-urban',
      },
      {
        name: 'Noah Bennett',
        role: 'Project Manager (Hobart)',
        content:
          'I was worried it would look fake, but the output still looks like me—just more professional. Great for LinkedIn.',
        style: 'executive',
      },
      {
        name: 'Grace Nolan',
        role: 'Small Business Owner (Devonport)',
        content:
          'I started with enhancements, then upgraded for headshots. The final set gave me consistent photos across platforms.',
        style: 'glamour',
      },
      {
        name: 'Ethan Price',
        role: 'Graduate Job Seeker (Kingston)',
        content:
          'Easy to use and the results came back in minutes. It helped me feel more confident sending out applications.',
        style: 'creative',
      },
    ];

    this.testimonials = testimonialData.map(testimonial => ({
      ...testimonial,
      imageUrl: this._stylePreviewService.getCachedUrl(testimonial.style),
    }));
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
    this._route.data.subscribe(data => {
      if (typeof window !== 'undefined') {
        const hasFragment = !!this._route.snapshot.fragment;
        const hasScrollTarget = !!data['scrollTo'];
        if (!hasFragment && !hasScrollTarget) {
          window.scrollTo({ top: 0, behavior: 'auto' });
        }
      }
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
          this._meta.updateTag({ name: 'description', content: data['meta']['description'] });
        }
        if (data['meta']['keywords']) {
          this._meta.updateTag({ name: 'keywords', content: data['meta']['keywords'] });
        }
      }
    });
  }

  onImageError(event: Event): void {
    const img = event.target as HTMLImageElement;

    // Extract style name from alt text or src URL
    const altText = img.alt || img.src || 'Professional Style';
    const styleName = this.extractStyleNameFromImageSource(altText);

    // Generate a unique, style-specific SVG placeholder
    const placeholderSvg = this.generateStyleSpecificPlaceholder(styleName);

    // Convert to base64 data URI
    img.src = 'data:image/svg+xml;base64,' + btoa(placeholderSvg);
  }

  private extractStyleNameFromImageSource(source: string): string {
    // Extract style name from various sources (alt text, URL, etc.)
    let styleName = 'Professional Style';

    if (source.includes('/style-previews/')) {
      // Extract from URL path
      const match = source.match(/\/style-previews\/([^.]+)/);
      if (match) {
        styleName = match[1].replace(/-/g, ' ').replace(/\b\w/g, l => l.toUpperCase());
      }
    } else {
      // Clean up alt text
      styleName =
        source
          .replace(' style example', '')
          .replace(' thumbnail', '')
          .replace('style-previews/', '')
          .replace(/\.(jpg|png|webp)$/, '')
          .replace(/-/g, ' ')
          .replace(/\b\w/g, l => l.toUpperCase()) || 'Professional Style';
    }

    return styleName;
  }

  private generateStyleSpecificPlaceholder(styleName: string): string {
    // Generate unique colors and patterns based on style name
    const colors = this.getStyleSpecificColors(styleName);
    const pattern = this.getStyleSpecificPattern(styleName);

    return `<svg width="400" height="400" xmlns="http://www.w3.org/2000/svg">
      <defs>
        <linearGradient id="grad-${this.hashCode(styleName)}" x1="0%" y1="0%" x2="100%" y2="100%">
          <stop offset="0%" style="stop-color:${colors.primary};stop-opacity:1" />
          <stop offset="100%" style="stop-color:${colors.secondary};stop-opacity:1" />
        </linearGradient>
        ${pattern.defs}
      </defs>
      <rect width="400" height="400" fill="url(#grad-${this.hashCode(styleName)})"/>
      ${pattern.elements}
      <text x="50%" y="85%" font-family="Arial, sans-serif" font-size="16" font-weight="500" fill="${colors.text}" text-anchor="middle" dominant-baseline="middle">${styleName}</text>
    </svg>`;
  }

  private getStyleSpecificColors(styleName: string): {
    primary: string;
    secondary: string;
    text: string;
  } {
    const hash = this.hashCode(styleName);
    const colorSchemes = [
      { primary: '#3B82F6', secondary: '#1D4ED8', text: '#E5E7EB' }, // Blue
      { primary: '#10B981', secondary: '#059669', text: '#E5E7EB' }, // Green
      { primary: '#8B5CF6', secondary: '#7C3AED', text: '#E5E7EB' }, // Purple
      { primary: '#F59E0B', secondary: '#D97706', text: '#1F2937' }, // Amber
      { primary: '#EF4444', secondary: '#DC2626', text: '#E5E7EB' }, // Red
      { primary: '#6B7280', secondary: '#4B5563', text: '#E5E7EB' }, // Gray
      { primary: '#EC4899', secondary: '#DB2777', text: '#E5E7EB' }, // Pink
      { primary: '#14B8A6', secondary: '#0D9488', text: '#E5E7EB' }, // Teal
    ];

    return colorSchemes[Math.abs(hash) % colorSchemes.length];
  }

  private getStyleSpecificPattern(styleName: string): { defs: string; elements: string } {
    const hash = this.hashCode(styleName);
    const patterns = [
      {
        defs: '',
        elements:
          '<circle cx="200" cy="150" r="60" fill="white" opacity="0.15"/><rect x="140" y="240" width="120" height="80" rx="8" fill="white" opacity="0.15"/>',
      },
      {
        defs: '',
        elements:
          '<polygon points="200,100 250,180 150,180" fill="white" opacity="0.12"/><rect x="160" y="250" width="80" height="100" rx="12" fill="white" opacity="0.12"/>',
      },
      {
        defs: '',
        elements:
          '<rect x="150" y="120" width="100" height="100" rx="50" fill="white" opacity="0.1"/><rect x="125" y="240" width="150" height="90" rx="15" fill="white" opacity="0.1"/>',
      },
      {
        defs: '',
        elements:
          '<ellipse cx="200" cy="160" rx="70" ry="50" fill="white" opacity="0.13"/><rect x="130" y="230" width="140" height="110" rx="10" fill="white" opacity="0.13"/>',
      },
    ];

    return patterns[Math.abs(hash) % patterns.length];
  }

  private hashCode(str: string): number {
    let hash = 0;
    for (let i = 0; i < str.length; i++) {
      const char = str.charCodeAt(i);
      hash = (hash << 5) - hash + char;
      hash = hash & hash; // Convert to 32-bit integer
    }
    return hash;
  }
}

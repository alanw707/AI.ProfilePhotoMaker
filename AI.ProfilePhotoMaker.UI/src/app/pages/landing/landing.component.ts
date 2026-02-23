import {
  AfterViewChecked,
  AfterViewInit,
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
  headshotCount: string;
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
  featured?: boolean;
  tall?: boolean;
  wide?: boolean;
}

interface BeforeAfterPair {
  id: number;
  before: string;
  after: string;
  label?: string;
}

interface SocialProofStats {
  totalHeadshots: number;
  averageRating: number;
  satisfactionGuarantee: number;
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
export class LandingComponent implements OnInit, AfterViewInit, AfterViewChecked, OnDestroy {
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
  private prefersReducedMotion = false;
  private scrollObserver: IntersectionObserver | null = null;
  private observedElements = new WeakSet<Element>();

  // Hero Before/After showcase
  heroBeforeAfterPairs: BeforeAfterPair[] = [];
  currentHeroPairIndex = 0;
  private heroRotationInterval: ReturnType<typeof setInterval> | null = null;
  private heroIntersectionObserver: IntersectionObserver | null = null;

  // Testimonial rotation
  private testimonialInterval: ReturnType<typeof setInterval> | null = null;

  // Interactive comparison slider
  sliderPosition = 50; // Percentage from left (0-100)
  isDraggingSlider = false;
  hasInteractedWithSlider = false;
  private boundMouseMove: ((e: MouseEvent) => void) | null = null;
  private boundTouchMove: ((e: TouchEvent) => void) | null = null;
  private boundDragEnd: (() => void) | null = null;
  private capturedPointerId: number | null = null;
  private pointerCaptureElement: HTMLElement | null = null;

  // Social proof stats (displayed in hero)
  socialProofStats: SocialProofStats = {
    totalHeadshots: 5000, // Will be updated from API
    averageRating: 4.8,
    satisfactionGuarantee: 100,
  };

  // Theme-related properties
  currentTheme$!: Observable<string>;
  private themeSubscription: Subscription = new Subscription();

  // Styled photos showcase - removed carousel, now using grid

  @ViewChild('comparisonSlider') comparisonSlider!: ElementRef;
  @ViewChild('heroSection') heroSection?: ElementRef;

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
      question: 'How do headshot packages work?',
      answer:
        'Choose a package based on how many headshots you need. Each package is a one-time purchase that includes AI model training and a set number of professional headshot generations. Your package never expires, so you can use it whenever you are ready.',
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
    this.initializeHeroBeforeAfter();
    this.initializeTestimonials();
    this.startTestimonialRotation();
    this.loadPackagesFromDatabase();
    this.loadAvailableStyles();
    this.observeElements();
    this.handleRouteData();
    this.prefersReducedMotion =
      typeof window !== 'undefined' &&
      window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  }

  ngAfterViewInit(): void {
    // Apply touch-action fix and pointer-events fix separately - they're independent of heroSection/reduced motion
    // Uses multiple timeouts to ensure Angular renders the *ngIf content first
    if (typeof window !== 'undefined') {
      setTimeout(() => this.applySliderTouchFix(), 200);
      setTimeout(() => this.applySliderTouchFix(), 500);
      setTimeout(() => this.applySliderTouchFix(), 1000);
      // Also apply pointer-events fix at various intervals to catch all render states
      setTimeout(() => this.applySliderPointerEventsFix(), 200);
      setTimeout(() => this.applySliderPointerEventsFix(), 500);
      setTimeout(() => this.applySliderPointerEventsFix(), 1000);
    }

    // Exit early for SSR, missing heroSection, or reduced motion preference
    if (typeof window === 'undefined' || !this.heroSection || this.prefersReducedMotion) {
      return;
    }

    this.heroIntersectionObserver = new IntersectionObserver(
      entries => {
        const isIntersecting = entries[0]?.isIntersecting ?? true;
        if (isIntersecting) {
          this.startHeroRotation();
        } else {
          this.stopHeroRotation();
        }
      },
      { threshold: 0.3 }
    );

    this.heroIntersectionObserver.observe(this.heroSection.nativeElement);
    this.startHeroRotation();
  }

  ngAfterViewChecked(): void {
    // Continuously reapply pointer-events fix after each change detection cycle
    // This is necessary because Angular may recreate DOM elements when hero images rotate
    this.applySliderPointerEventsFix();
  }

  /**
   * Lightweight fix that only sets pointer-events on comparison overlays.
   * Called on every change detection cycle to ensure the fix persists.
   */
  private applySliderPointerEventsFix(): void {
    if (typeof window === 'undefined') {
      return;
    }

    const beforeEl = document.querySelector('.comparison-before') as HTMLElement;
    const afterEl = document.querySelector('.comparison-after') as HTMLElement;

    // Use setProperty with 'important' for better cross-browser compatibility
    if (beforeEl) {
      beforeEl.style.setProperty('pointer-events', 'none', 'important');
    }
    if (afterEl) {
      afterEl.style.setProperty('pointer-events', 'none', 'important');
    }
  }

  /**
   * Applies touch-action: none directly to the slider element.
   * This ensures the slider can be dragged on touch devices without
   * browser scroll interference.
   * Uses both ViewChild and direct DOM query for reliability.
   */
  private applySliderTouchFix(): void {
    // Try ViewChild first
    let sliderEl = this.comparisonSlider?.nativeElement as HTMLElement | null;

    // Fallback to direct DOM query if ViewChild not ready
    if (!sliderEl) {
      sliderEl = document.querySelector('.comparison-slider') as HTMLElement | null;
    }

    if (sliderEl) {
      sliderEl.style.touchAction = 'none';

      // Also apply to the slider handle if it exists
      const handleEl = sliderEl.querySelector('.slider-handle') as HTMLElement;
      if (handleEl) {
        handleEl.style.touchAction = 'none';
      }

      // Fix pointer-events on overlay images so they don't intercept slider drag events
      const beforeEl = sliderEl.querySelector('.comparison-before') as HTMLElement;
      const afterEl = sliderEl.querySelector('.comparison-after') as HTMLElement;
      if (beforeEl) {
        beforeEl.style.pointerEvents = 'none';
      }
      if (afterEl) {
        afterEl.style.pointerEvents = 'none';
      }
    }
  }

  ngOnDestroy(): void {
    this.themeSubscription.unsubscribe();

    if (this.heroRotationInterval) {
      clearInterval(this.heroRotationInterval);
    }

    if (this.testimonialInterval) {
      clearInterval(this.testimonialInterval);
    }

    if (this.heroIntersectionObserver) {
      this.heroIntersectionObserver.disconnect();
    }

    if (this.scrollObserver) {
      this.scrollObserver.disconnect();
    }

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

  // Hero Before/After Methods
  private initializeHeroBeforeAfter(): void {
    // Map image sets to style labels based on actual content:
    // set-1: urban/creative night scene → LinkedIn Ready
    // set-2: casual athletic wear → Business Casual
    // set-3: formal suit with tie → Corporate
    this.heroBeforeAfterPairs = [
      {
        id: 1,
        before: 'assets/marketing/before-after/beach-vibes-before.jpg',
        after: 'assets/marketing/before-after/beach-vibes-after.jpg',
        label: 'Beach Vibes',
      },
      {
        id: 2,
        before: 'assets/marketing/before-after/linkedin-before.jpeg',
        after: 'assets/marketing/before-after/linkedin-after.jpg',
        label: 'LinkedIn Professional',
      },
      {
        id: 3,
        before: 'assets/marketing/before-after/executive-before.jpeg',
        after: 'assets/marketing/before-after/executive-after.jpg',
        label: 'Executive',
      },
      {
        id: 4,
        before: 'assets/marketing/before-after/medical-before.jpg',
        after: 'assets/marketing/before-after/medical.jpg',
        label: 'Medical Professional',
      },
      {
        id: 5,
        before: 'assets/marketing/before-after/academic1-before.jpg',
        after: 'assets/marketing/before-after/academic1-after.jpg',
        label: 'Academic',
      },
      {
        id: 6,
        before: 'assets/marketing/before-after/academic2-before.jpg',
        after: 'assets/marketing/before-after/academic2-after.jpg',
        label: 'Academic Scholar',
      },
      {
        id: 7,
        before: 'assets/marketing/before-after/set-1-before.jpg',
        after: 'assets/marketing/before-after/set-1-after.jpg',
        label: 'LinkedIn Ready',
      },
    ];
  }

  private startHeroRotation(): void {
    if (this.prefersReducedMotion || this.heroBeforeAfterPairs.length === 0) {
      return;
    }

    this.stopHeroRotation();
    this.heroRotationInterval = setInterval(() => {
      this.currentHeroPairIndex =
        (this.currentHeroPairIndex + 1) % this.heroBeforeAfterPairs.length;
      this._cdr.detectChanges();
      // Reapply touch-action fix after Angular renders new content
      setTimeout(() => this.applySliderTouchFix(), 50);
    }, 4000);
  }

  private stopHeroRotation(): void {
    if (this.heroRotationInterval) {
      clearInterval(this.heroRotationInterval);
      this.heroRotationInterval = null;
    }
  }

  selectHeroPair(index: number): void {
    this.currentHeroPairIndex = index;
    // Reset the rotation timer when manually selected
    if (!this.prefersReducedMotion) {
      this.startHeroRotation();
    }
    // Reapply touch-action fix after Angular renders the new content
    setTimeout(() => this.applySliderTouchFix(), 50);
  }

  get currentHeroPair(): BeforeAfterPair | null {
    return this.heroBeforeAfterPairs[this.currentHeroPairIndex] || null;
  }

  // Comparison slider interaction methods
  startSliderDrag(event: MouseEvent | TouchEvent | PointerEvent): void {
    // Prevent duplicate handling - if pointerdown fires, mousedown/touchstart also fire
    // Only handle the first event type that arrives
    if (this.isDraggingSlider) {
      return;
    }

    event.preventDefault();
    this.isDraggingSlider = true;
    this.hasInteractedWithSlider = true;

    // Stop auto-rotation while interacting
    this.stopHeroRotation();

    // CRITICAL: For PointerEvents, capture the pointer to ensure we receive all
    // subsequent pointermove events even if the pointer leaves the element.
    // This is essential for Windows browsers with touch-capable screens.
    // We capture on the slider container, not the event target (which might be the SVG icon).
    if (event instanceof PointerEvent && this.comparisonSlider?.nativeElement) {
      try {
        const sliderEl = this.comparisonSlider.nativeElement as HTMLElement;
        sliderEl.setPointerCapture(event.pointerId);
        this.capturedPointerId = event.pointerId;
        this.pointerCaptureElement = sliderEl;
      } catch {
        // setPointerCapture may fail in some edge cases, continue with fallback
      }
    }

    // Bind event listeners for drag - use pointer events as primary (modern browsers)
    // with mouse/touch as fallback
    this.boundMouseMove = (e: MouseEvent) => this.handleSliderMove(e);
    this.boundTouchMove = (e: TouchEvent) => this.handleSliderMove(e);
    this.boundDragEnd = () => this.endSliderDrag();

    // Pointer events (modern, unified input)
    document.addEventListener('pointermove', this.boundMouseMove as EventListener);
    document.addEventListener('pointerup', this.boundDragEnd);
    document.addEventListener('pointercancel', this.boundDragEnd);
    // Mouse events (fallback)
    document.addEventListener('mousemove', this.boundMouseMove);
    document.addEventListener('mouseup', this.boundDragEnd);
    // Touch events (fallback)
    document.addEventListener('touchmove', this.boundTouchMove, { passive: false });
    document.addEventListener('touchend', this.boundDragEnd);

    // Handle initial position
    this.handleSliderMove(event);
  }

  private handleSliderMove(event: MouseEvent | TouchEvent): void {
    if (!this.isDraggingSlider || !this.comparisonSlider) {
      return;
    }

    const sliderEl = this.comparisonSlider.nativeElement as HTMLElement;
    const rect = sliderEl.getBoundingClientRect();

    let clientX: number;
    if (event instanceof TouchEvent) {
      clientX = event.touches[0]?.clientX ?? 0;
    } else {
      clientX = event.clientX;
    }

    // Calculate position as percentage
    const x = clientX - rect.left;
    const percentage = Math.max(0, Math.min(100, (x / rect.width) * 100));

    this.sliderPosition = percentage;
    this._cdr.detectChanges();
  }

  private endSliderDrag(): void {
    this.isDraggingSlider = false;

    // Release pointer capture if we had one
    if (this.capturedPointerId !== null && this.pointerCaptureElement) {
      try {
        this.pointerCaptureElement.releasePointerCapture(this.capturedPointerId);
      } catch {
        // releasePointerCapture may fail if already released, ignore
      }
      this.capturedPointerId = null;
      this.pointerCaptureElement = null;
    }

    // Clean up all event listeners (pointer, mouse, and touch)
    if (this.boundMouseMove) {
      document.removeEventListener('pointermove', this.boundMouseMove as EventListener);
      document.removeEventListener('mousemove', this.boundMouseMove);
    }
    if (this.boundTouchMove) {
      document.removeEventListener('touchmove', this.boundTouchMove);
    }
    if (this.boundDragEnd) {
      document.removeEventListener('pointerup', this.boundDragEnd);
      document.removeEventListener('pointercancel', this.boundDragEnd);
      document.removeEventListener('mouseup', this.boundDragEnd);
      document.removeEventListener('touchend', this.boundDragEnd);
    }

    // Resume auto-rotation after a delay
    setTimeout(() => {
      if (!this.isDraggingSlider) {
        this.startHeroRotation();
      }
    }, 3000);
  }

  adjustSlider(delta: number): void {
    this.hasInteractedWithSlider = true;
    this.sliderPosition = Math.max(0, Math.min(100, this.sliderPosition + delta));
    this._cdr.detectChanges();
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
            headshotCount: this.formatHeadshotCount(pkg.totalCredits),
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
        // Force change detection so Angular renders the pricing cards, then
        // re-scan for dynamically added .animate-on-scroll elements.
        this._cdr.detectChanges();
        setTimeout(() => this.observeNewElements(), 100);
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
        headshotCount: this.formatHeadshotCount(50),
      },
      {
        name: 'Professional',
        price: '$19',
        originalPrice: '$29',
        features: this.buildPackageFeatures('Professional', 150),
        recommended: true,
        headshotCount: this.formatHeadshotCount(150),
      },
      {
        name: 'Studio',
        price: '$39',
        features: this.buildPackageFeatures('Studio', 400),
        headshotCount: this.formatHeadshotCount(400),
      },
    ];
  }

  private formatHeadshotCount(totalCredits: number): string {
    const count = this.getStyledGenerationCount(totalCredits);
    if (count === 0) {
      return 'Model training credits';
    }
    return `Up to ${count} headshots`;
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
      content: 'https://aiprofilephotomaker.com/assets/og-image.png?v=3',
    });
    this._meta.updateTag({
      property: 'og:image:secure_url',
      content: 'https://aiprofilephotomaker.com/assets/og-image.png?v=3',
    });
    this._meta.updateTag({ property: 'og:image:type', content: 'image/png' });
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
      content: 'https://aiprofilephotomaker.com/assets/og-image.png?v=3',
    });
    this._meta.updateTag({
      name: 'twitter:image:alt',
      content: 'AI Profile Photo Maker preview card',
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
      screenshot: 'https://aiprofilephotomaker.com/assets/og-image.png?v=3',
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
      datePublished: '2025-01-01',
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
      // Re-scan for dynamically rendered animate-on-scroll elements (style cards)
      setTimeout(() => this.observeNewElements(), 100);
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
    this.styledPhotos = this.availableStyles.slice(0, 20).map((style, index) => ({
      id: style.id,
      imageUrl: this._stylePreviewService.getCachedUrl(style.name),
      style: style.name,
      category: this.getCategoryFromStyleName(style.name),
      description: style.description,
      // Assign featured status to first 3 styles for visual emphasis
      featured: index < 3,
      // Create varied heights - every 5th and 6th card is tall
      tall: index % 7 === 4 || index % 7 === 5,
      // Create varied widths - every 8th card is wide
      wide: index % 8 === 7,
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
      // Assign featured status to first 3 styles for visual emphasis
      featured: index < 3,
      // Create varied heights - every 5th and 6th card is tall
      tall: index % 7 === 4 || index % 7 === 5,
      // Create varied widths - every 8th card is wide
      wide: index % 8 === 7,
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
    if (this.testimonials.length === 0) {
      return;
    }
    this.testimonialInterval = setInterval(() => {
      this.currentTestimonialIndex = (this.currentTestimonialIndex + 1) % this.testimonials.length;
    }, 5000);
  }

  private initializeTestimonials(): void {
    this.testimonials = [
      {
        name: 'Prof. Richard',
        role: 'University Professor',
        content:
          "I needed a professional headshot for my department page but didn't want to sit through a studio session. Uploaded a photo from my living room and got back something that looks like it belongs in a faculty directory. My students barely recognized me.",
        style: 'academic',
        imageUrl: 'assets/marketing/before-after/academic1-after.jpg',
      },
      {
        name: 'David',
        role: 'Postdoctoral Researcher',
        content:
          "As a researcher applying for faculty positions, first impressions matter. This tool turned my phone selfie into a polished headshot that I'm now using on my CV, lab website, and conference profiles. Saved me a trip to the photographer.",
        style: 'academic',
        imageUrl: 'assets/marketing/before-after/academic2-after.jpg',
      },
      {
        name: 'Ashley',
        role: 'Content Creator',
        content:
          'I wanted a natural, sun-kissed look for my dating profile without hiring a photographer. The beach vibes style nailed exactly what I was going for. My friends thought I had a professional shoot done in Malibu!',
        style: 'dating',
        imageUrl: 'assets/marketing/before-after/beach-vibes-after.jpg',
      },
      {
        name: 'Raj',
        role: 'VP of Operations',
        content:
          'Our entire leadership team needed updated headshots for the annual report. Instead of coordinating schedules with a photographer, everyone uploaded a casual photo and we had boardroom-ready portraits within the hour. The ROI on time alone was incredible.',
        style: 'executive',
        imageUrl: 'assets/marketing/before-after/executive-after.jpg',
      },
      {
        name: 'Brianna',
        role: 'Marketing Manager',
        content:
          "I was between jobs and needed a sharp LinkedIn photo fast. Didn't have time or budget for a professional session. The result looked so polished that two recruiters commented on my profile photo during interviews. Worth every penny.",
        style: 'linkedin',
        imageUrl: 'assets/marketing/before-after/linkedin-after.jpg',
      },
      {
        name: 'Dr. Sofia',
        role: 'Family Medicine Physician',
        content:
          'Patients want to see a friendly, trustworthy face before booking an appointment. My old headshot was from residency. This gave me a clean, professional medical portrait that I now use on the clinic website and my health network profiles.',
        style: 'medical',
        imageUrl: 'assets/marketing/before-after/medical.jpg',
      },
    ];
  }

  observeElements(): void {
    this.scrollObserver = new IntersectionObserver(
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
      this.observeNewElements();
    }, 100);
  }

  /**
   * Scans for any `.animate-on-scroll` elements not yet observed and adds them
   * to the IntersectionObserver. Elements already visible in the viewport are
   * immediately animated in (the observer may not fire for elements that were
   * added while already in view). Safe to call multiple times (e.g. after async
   * content like pricing packages finishes loading).
   */
  private observeNewElements(): void {
    if (!this.scrollObserver) {
      return;
    }
    const elements = document.querySelectorAll('.animate-on-scroll');
    elements.forEach(el => {
      if (!this.observedElements.has(el)) {
        this.observedElements.add(el);
        this.scrollObserver!.observe(el);

        // Fallback: if the element is already in the viewport, animate it
        // immediately. IntersectionObserver may not fire for elements that
        // appear while already intersecting.
        const rect = el.getBoundingClientRect();
        const inViewport =
          rect.top < window.innerHeight &&
          rect.bottom > 0 &&
          rect.left < window.innerWidth &&
          rect.right > 0;
        if (inViewport) {
          el.classList.add('animate-in');
        }
      }
    });
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

    this.themeSubscription.add(
      this._route.fragment.subscribe(fragment => {
        if (fragment) {
          setTimeout(() => this.navigation.scrollToSection(fragment), 120);
        }
      })
    );
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

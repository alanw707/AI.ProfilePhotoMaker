import {
  Directive,
  ElementRef,
  Input,
  OnInit,
  OnDestroy,
  inject,
  PLATFORM_ID,
  Renderer2,
  NgZone,
} from '@angular/core';
import { isPlatformBrowser } from '@angular/common';

/**
 * Directive to trigger entrance animations when elements scroll into view.
 * Uses IntersectionObserver for performance.
 *
 * Usage:
 * <div appAnimateOnScroll>...</div>
 * <div appAnimateOnScroll [delay]="2">...</div>  // adds delay-2 class
 * <div appAnimateOnScroll [threshold]="0.3">...</div>  // 30% visible before triggering
 *
 * The directive adds 'animate-on-scroll' class initially, then 'animate-in' when visible.
 * CSS handles the actual animation (defined in seo-page.component.sass and _redesign-system.sass).
 */
@Directive({
  selector: '[appAnimateOnScroll]',
  standalone: true,
})
export class AnimateOnScrollDirective implements OnInit, OnDestroy {
  private readonly el = inject(ElementRef);
  private readonly renderer = inject(Renderer2);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly ngZone = inject(NgZone);

  private observer: IntersectionObserver | null = null;

  /**
   * Stagger delay (1-8). Adds 'delay-N' class for staggered grid animations.
   */
  @Input() delay: number | null = null;

  /**
   * Intersection threshold (0-1). How much of the element must be visible.
   * Default: 0.1 (10% visible triggers animation)
   */
  @Input() threshold = 0.1;

  /**
   * Root margin for earlier/later triggering.
   * Default: '0px 0px -50px 0px' (trigger 50px before element enters viewport)
   */
  @Input() rootMargin = '0px 0px -50px 0px';

  /**
   * Whether to animate only once or re-animate when scrolling back.
   * Default: true (animate once)
   */
  @Input() once = true;

  ngOnInit(): void {
    if (!isPlatformBrowser(this.platformId)) {
      // SSR: Don't set up observer, but add animate-in class immediately
      this.renderer.addClass(this.el.nativeElement, 'animate-in');
      return;
    }

    // Respect reduced motion preference
    const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    if (prefersReducedMotion) {
      // Skip animation setup, show element immediately
      this.renderer.addClass(this.el.nativeElement, 'animate-in');
      return;
    }

    // Add initial classes
    this.renderer.addClass(this.el.nativeElement, 'animate-on-scroll');

    if (this.delay !== null && this.delay >= 1 && this.delay <= 8) {
      this.renderer.addClass(this.el.nativeElement, `delay-${this.delay}`);
    }

    // Set up IntersectionObserver
    this.setupObserver();
  }

  ngOnDestroy(): void {
    if (this.observer) {
      this.observer.disconnect();
      this.observer = null;
    }
  }

  private setupObserver(): void {
    if (typeof IntersectionObserver === 'undefined') {
      // Fallback for browsers without IntersectionObserver support
      this.renderer.addClass(this.el.nativeElement, 'animate-in');
      return;
    }

    // Clamp threshold to valid range (0-1)
    const clampedThreshold = Math.max(0, Math.min(1, this.threshold));

    const options: IntersectionObserverInit = {
      threshold: clampedThreshold,
      rootMargin: this.rootMargin,
    };

    // Create observer outside Angular zone for performance
    this.ngZone.runOutsideAngular(() => {
      this.observer = new IntersectionObserver(entries => {
        entries.forEach(entry => {
          // Run DOM updates inside Angular zone to trigger change detection
          this.ngZone.run(() => {
            if (entry.isIntersecting) {
              this.renderer.addClass(this.el.nativeElement, 'animate-in');

              if (this.once && this.observer) {
                this.observer.unobserve(entry.target);
              }
            } else if (!this.once) {
              this.renderer.removeClass(this.el.nativeElement, 'animate-in');
            }
          });
        });
      }, options);

      this.observer.observe(this.el.nativeElement);
    });
  }
}

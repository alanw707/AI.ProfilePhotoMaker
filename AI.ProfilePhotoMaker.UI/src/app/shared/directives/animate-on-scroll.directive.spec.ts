import { Component, DebugElement, NgZone } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { AnimateOnScrollDirective } from './animate-on-scroll.directive';

// Test host component
@Component({
  template: `
    <div appAnimateOnScroll class="test-element">Content</div>
    <div appAnimateOnScroll [delay]="3" class="delayed-element">Delayed</div>
    <div appAnimateOnScroll [threshold]="0.5" class="threshold-element">Threshold</div>
    <div appAnimateOnScroll [once]="false" class="repeat-element">Repeat</div>
  `,
  standalone: true,
  imports: [AnimateOnScrollDirective],
})
class TestHostComponent {}

describe('AnimateOnScrollDirective', () => {
  let fixture: ComponentFixture<TestHostComponent>;
  let testElement: DebugElement;
  let delayedElement: DebugElement;
  let ngZone: NgZone;

  // Mock IntersectionObserver storage
  interface ObserverInstance {
    callback: IntersectionObserverCallback;
    options: IntersectionObserverInit;
    observe: jasmine.Spy;
    unobserve: jasmine.Spy;
    disconnect: jasmine.Spy;
  }
  let observerInstances: ObserverInstance[] = [];
  let originalIntersectionObserver: typeof IntersectionObserver;

  beforeEach(async () => {
    // Reset observer instances
    observerInstances = [];

    // Save original
    originalIntersectionObserver = window.IntersectionObserver;

    // Setup IntersectionObserver mock that stores each instance
    // eslint-disable-next-line @typescript-eslint/naming-convention
    (window as { IntersectionObserver: unknown }).IntersectionObserver =
      class MockIntersectionObserver {
        private instanceData: ObserverInstance;

        constructor(callback: IntersectionObserverCallback, options?: IntersectionObserverInit) {
          this.instanceData = {
            callback,
            options: options || {},
            observe: jasmine.createSpy('observe'),
            unobserve: jasmine.createSpy('unobserve'),
            disconnect: jasmine.createSpy('disconnect'),
          };
          observerInstances.push(this.instanceData);
        }
        observe(target: Element) {
          this.instanceData.observe(target);
        }
        unobserve(target: Element) {
          this.instanceData.unobserve(target);
        }
        disconnect() {
          this.instanceData.disconnect();
        }
      } as unknown as typeof IntersectionObserver;

    await TestBed.configureTestingModule({
      imports: [TestHostComponent],
    }).compileComponents();

    ngZone = TestBed.inject(NgZone);
    fixture = TestBed.createComponent(TestHostComponent);
    fixture.detectChanges();

    testElement = fixture.debugElement.query(By.css('.test-element'));
    delayedElement = fixture.debugElement.query(By.css('.delayed-element'));
  });

  afterEach(() => {
    // Restore original
    window.IntersectionObserver = originalIntersectionObserver;
  });

  it('should create the directive', () => {
    const directive = testElement.injector.get(AnimateOnScrollDirective);
    expect(directive).toBeTruthy();
  });

  it('should add animate-on-scroll class on init', () => {
    expect(testElement.nativeElement.classList.contains('animate-on-scroll')).toBe(true);
  });

  it('should add delay class when delay input is provided', () => {
    expect(delayedElement.nativeElement.classList.contains('delay-3')).toBe(true);
  });

  it('should not add delay class when delay is null', () => {
    const classes = Array.from(testElement.nativeElement.classList) as string[];
    const hasDelayClass = classes.some(c => c.startsWith('delay-'));
    expect(hasDelayClass).toBe(false);
  });

  it('should call observe on the element', () => {
    // First observer instance is for the test-element
    expect(observerInstances[0].observe).toHaveBeenCalled();
  });

  it('should use default threshold of 0.1', () => {
    expect(observerInstances[0].options.threshold).toBe(0.1);
  });

  it('should use default rootMargin', () => {
    expect(observerInstances[0].options.rootMargin).toBe('0px 0px -50px 0px');
  });

  it('should add animate-in class when element intersects', () => {
    const instance = observerInstances[0];
    const mockEntry = {
      isIntersecting: true,
      target: testElement.nativeElement,
    } as IntersectionObserverEntry;

    // Run callback inside ngZone to simulate real behavior
    ngZone.run(() => {
      instance.callback([mockEntry], {} as IntersectionObserver);
    });
    fixture.detectChanges();

    expect(testElement.nativeElement.classList.contains('animate-in')).toBe(true);
  });

  it('should disconnect observer on destroy', () => {
    const disconnectSpy = observerInstances[0].disconnect;
    fixture.destroy();
    expect(disconnectSpy).toHaveBeenCalled();
  });

  describe('with delay values', () => {
    it('should add delay-3 class for delay=3', () => {
      expect(delayedElement.nativeElement.classList.contains('delay-3')).toBe(true);
    });

    it('should not add delay class for values outside 1-8 range', () => {
      @Component({
        template: `<div appAnimateOnScroll [delay]="10" class="invalid-delay">Invalid</div>`,
        standalone: true,
        imports: [AnimateOnScrollDirective],
      })
      class InvalidDelayTestComponent {}

      const invalidFixture = TestBed.createComponent(InvalidDelayTestComponent);
      invalidFixture.detectChanges();

      const element = invalidFixture.debugElement.query(By.css('.invalid-delay'));
      const classes = Array.from(element.nativeElement.classList) as string[];
      const hasDelayClass = classes.some(c => c.startsWith('delay-'));
      expect(hasDelayClass).toBe(false);
    });

    it('should not add delay class for delay=0', () => {
      @Component({
        template: `<div appAnimateOnScroll [delay]="0" class="zero-delay">Zero</div>`,
        standalone: true,
        imports: [AnimateOnScrollDirective],
      })
      class ZeroDelayTestComponent {}

      const zeroFixture = TestBed.createComponent(ZeroDelayTestComponent);
      zeroFixture.detectChanges();

      const element = zeroFixture.debugElement.query(By.css('.zero-delay'));
      const classes = Array.from(element.nativeElement.classList) as string[];
      const hasDelayClass = classes.some(c => c.startsWith('delay-'));
      expect(hasDelayClass).toBe(false);
    });
  });

  describe('reduced motion preference', () => {
    it('should add animate-in immediately when prefers-reduced-motion is set', () => {
      // Mock matchMedia to return prefers-reduced-motion: reduce
      spyOn(window, 'matchMedia').and.callFake((query: string) => ({
        matches: query === '(prefers-reduced-motion: reduce)',
        media: query,
        onchange: null,
        addListener: () => {},
        removeListener: () => {},
        addEventListener: () => {},
        removeEventListener: () => {},
        dispatchEvent: () => true,
      }));

      @Component({
        template: `<div appAnimateOnScroll class="reduced-motion-element">Reduced</div>`,
        standalone: true,
        imports: [AnimateOnScrollDirective],
      })
      class ReducedMotionTestComponent {}

      const reducedMotionFixture = TestBed.createComponent(ReducedMotionTestComponent);
      reducedMotionFixture.detectChanges();

      const element = reducedMotionFixture.debugElement.query(By.css('.reduced-motion-element'));
      expect(element.nativeElement.classList.contains('animate-in')).toBe(true);

      // Should NOT have animate-on-scroll class (skipped setup)
      expect(element.nativeElement.classList.contains('animate-on-scroll')).toBe(false);
    });
  });

  describe('custom threshold', () => {
    it('should use custom threshold when provided', () => {
      // threshold-element has threshold=0.5, it's the 3rd element (index 2)
      expect(observerInstances[2].options.threshold).toBe(0.5);
    });

    it('should clamp threshold to max 1', () => {
      @Component({
        template: `<div appAnimateOnScroll [threshold]="1.5" class="over-threshold">Over</div>`,
        standalone: true,
        imports: [AnimateOnScrollDirective],
      })
      class OverThresholdTestComponent {}

      const initialCount = observerInstances.length;
      const overFixture = TestBed.createComponent(OverThresholdTestComponent);
      overFixture.detectChanges();

      const newInstance = observerInstances[initialCount];
      expect(newInstance.options.threshold).toBe(1);
    });

    it('should clamp threshold to min 0', () => {
      @Component({
        template: `<div appAnimateOnScroll [threshold]="-0.5" class="under-threshold">Under</div>`,
        standalone: true,
        imports: [AnimateOnScrollDirective],
      })
      class UnderThresholdTestComponent {}

      const initialCount = observerInstances.length;
      const underFixture = TestBed.createComponent(UnderThresholdTestComponent);
      underFixture.detectChanges();

      const newInstance = observerInstances[initialCount];
      expect(newInstance.options.threshold).toBe(0);
    });
  });

  describe('once=false behavior', () => {
    it('should remove animate-in class when element leaves viewport if once=false', () => {
      @Component({
        template: `<div appAnimateOnScroll [once]="false" class="repeat-test">Repeat</div>`,
        standalone: true,
        imports: [AnimateOnScrollDirective],
      })
      class RepeatTestComponent {}

      const initialCount = observerInstances.length;
      const repeatFixture = TestBed.createComponent(RepeatTestComponent);
      repeatFixture.detectChanges();

      const element = repeatFixture.debugElement.query(By.css('.repeat-test'));
      const instance = observerInstances[initialCount];

      // First, element enters viewport
      ngZone.run(() => {
        instance.callback(
          [{ isIntersecting: true, target: element.nativeElement } as IntersectionObserverEntry],
          {} as IntersectionObserver
        );
      });
      repeatFixture.detectChanges();

      expect(element.nativeElement.classList.contains('animate-in')).toBe(true);

      // Then element leaves viewport
      ngZone.run(() => {
        instance.callback(
          [{ isIntersecting: false, target: element.nativeElement } as IntersectionObserverEntry],
          {} as IntersectionObserver
        );
      });
      repeatFixture.detectChanges();

      expect(element.nativeElement.classList.contains('animate-in')).toBe(false);
    });
  });
});

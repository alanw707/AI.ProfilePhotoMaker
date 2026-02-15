import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { Meta, Title } from '@angular/platform-browser';
import { ChangeDetectorRef } from '@angular/core';
import { LandingComponent } from './landing.component';
import { NavigationService } from '../../services/navigation.service';
import { ThemeService } from '../../services/theme.service';
import { AuthService } from '../../services/auth.service';
import { CreditService } from '../../services/credit.service';
import { StyleService } from '../../services/style.service';
import { ConfigService } from '../../services/config.service';
import { StylePreviewService } from '../../services/style-preview.service';
import { CookieConsentService } from '../../services/cookie-consent.service';

describe('LandingComponent styles grid', () => {
  let fixture: ComponentFixture<LandingComponent>;
  let component: LandingComponent;

  beforeEach(async () => {
    const navigationSpy = jasmine.createSpyObj<NavigationService>('NavigationService', [
      'getCurrentRoute',
      'navigateToSection',
      'navigateTo',
      'scrollToSection',
      'goToEnhance',
    ]);
    navigationSpy.getCurrentRoute.and.returnValue('/');
    navigationSpy.navigateToSection.and.returnValue(Promise.resolve(true));
    navigationSpy.navigateTo.and.returnValue(Promise.resolve(true));
    navigationSpy.goToEnhance.and.returnValue(Promise.resolve(true));

    const themeServiceMock = {
      theme$: of('light'),
      toggleTheme: jasmine.createSpy('toggleTheme'),
    };

    const authServiceSpy = jasmine.createSpyObj<AuthService>('AuthService', ['isAuthenticated']);
    authServiceSpy.isAuthenticated.and.returnValue(false);
    (authServiceSpy as any).isAuthenticated$ = of(false);

    const creditServiceSpy = jasmine.createSpyObj<CreditService>('CreditService', [
      'getCreditCosts',
      'getCreditCostSync',
    ]);
    creditServiceSpy.getCreditCosts.and.returnValue(
      of({ success: false, data: null, error: null } as any)
    );
    creditServiceSpy.getCreditCostSync.and.returnValue(5);

    const styleServiceSpy = jasmine.createSpyObj<StyleService>('StyleService', ['getActiveStyles']);
    styleServiceSpy.getActiveStyles.and.returnValue(
      of({ success: true, data: [], error: null } as any)
    );

    const stylePreviewSpy = jasmine.createSpyObj<StylePreviewService>('StylePreviewService', [
      'getCachedUrl',
      'getAllPreviewUrls',
    ]);
    stylePreviewSpy.getCachedUrl.and.callFake(
      (style: string) => `https://example.test/${style}.jpg`
    );
    stylePreviewSpy.getAllPreviewUrls.and.returnValue(of(new Map()));

    await TestBed.configureTestingModule({
      imports: [LandingComponent],
      providers: [
        { provide: ActivatedRoute, useValue: { data: of({}), snapshot: { fragment: null } } },
        { provide: NavigationService, useValue: navigationSpy },
        { provide: ThemeService, useValue: themeServiceMock },
        { provide: AuthService, useValue: authServiceSpy },
        { provide: CreditService, useValue: creditServiceSpy },
        { provide: StyleService, useValue: styleServiceSpy },
        { provide: ConfigService, useValue: {} },
        { provide: StylePreviewService, useValue: stylePreviewSpy },
        { provide: CookieConsentService, useValue: { requestPreferencesOpen: () => {} } },
        { provide: Meta, useValue: jasmine.createSpyObj<Meta>('Meta', ['updateTag', 'addTag']) },
        { provide: Title, useValue: jasmine.createSpyObj<Title>('Title', ['setTitle']) },
        { provide: ChangeDetectorRef, useValue: { detectChanges: () => {} } },
        provideRouter([]),
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(LandingComponent);
    component = fixture.componentInstance;

    spyOn(component, 'ngOnInit').and.callFake(() => {});
    spyOn(component, 'ngAfterViewInit').and.callFake(() => {});
    spyOn(component, 'ngAfterViewChecked').and.callFake(() => {});
  });

  it('renders 20 style cards when 20 styles are available', () => {
    component.showNotFound = false;
    component.isLoadingStyles = false;
    component.styledPhotos = Array.from({ length: 20 }, (_, i) => ({
      id: i + 1,
      imageUrl: `https://example.test/style-${i + 1}.jpg`,
      style: `style-${i + 1}`,
      category: 'Test',
      description: `style ${i + 1}`,
    }));

    fixture.detectChanges();

    const cards = fixture.nativeElement.querySelectorAll('.styles-image-grid .style-card-minimal');
    expect(cards.length).toBe(20);
  });
});

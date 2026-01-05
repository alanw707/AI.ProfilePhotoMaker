import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { RouterTestingModule } from '@angular/router/testing';
import { BehaviorSubject, of } from 'rxjs';
import { LoginComponent } from './login.component';
import { AuthService } from '../../services/auth.service';
import { ConfigService } from '../../services/config.service';
import { ThemeService } from '../../services/theme.service';

describe('LoginComponent', () => {
  let component: LoginComponent;
  let fixture: ComponentFixture<LoginComponent>;
  let authService: jasmine.SpyObj<AuthService>;
  let router: Router;
  let isAuthenticatedSubject: BehaviorSubject<boolean>;

  beforeEach(async () => {
    isAuthenticatedSubject = new BehaviorSubject(false);
    authService = jasmine.createSpyObj<AuthService>('AuthService', [
      'isAuthenticated',
      'ensureSession',
      'login',
      'handleOAuthCallback',
    ]);

    authService.isAuthenticated.and.returnValue(false);
    authService.ensureSession.and.returnValue(of(false));
    authService.login.and.returnValue(
      of({ token: '', email: '', firstName: '', lastName: '' } as any)
    );
    (authService as any).isAuthenticated$ = isAuthenticatedSubject.asObservable();

    const themeService = jasmine.createSpyObj<ThemeService>('ThemeService', ['toggleTheme', 'isDark']);
    themeService.isDark.and.returnValue(false);

    const configService = jasmine.createSpyObj<ConfigService>('ConfigService', ['getOAuthBaseUrl']);
    configService.getOAuthBaseUrl.and.returnValue('http://localhost:5032');

    const route = {
      snapshot: { queryParams: {} },
      queryParams: of({}),
    };

    await TestBed.configureTestingModule({
      imports: [LoginComponent, RouterTestingModule],
      providers: [
        { provide: AuthService, useValue: authService },
        { provide: ThemeService, useValue: themeService },
        { provide: ConfigService, useValue: configService },
        { provide: ActivatedRoute, useValue: route },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    fixture.detectChanges();
  });

  it('disables submit when age confirmation is missing', () => {
    component.loginForm.setValue({
      email: 'test@example.com',
      password: 'Password123!',
      ageConfirmed: false,
    });

    fixture.detectChanges();

    const button: HTMLButtonElement | null = fixture.nativeElement.querySelector(
      'button[type="submit"]'
    );

    expect(button).withContext('submit button should exist').not.toBeNull();
    expect(button?.disabled).toBeTrue();
    expect(button?.getAttribute('aria-disabled')).toBe('true');
  });

  it('marks age confirmation as required on submit attempt', () => {
    component.loginForm.setValue({
      email: 'test@example.com',
      password: 'Password123!',
      ageConfirmed: false,
    });

    component.onSubmit();
    fixture.detectChanges();

    expect(authService.login).not.toHaveBeenCalled();
    expect(component.f['ageConfirmed'].touched).toBeTrue();

    const error = fixture.nativeElement.querySelector(`#${component.ageConfirmErrorId}`);
    expect(error?.textContent).toContain(component.ageConfirmationMessage);
  });

  it('submits login when age confirmation is true', () => {
    component.loginForm.setValue({
      email: 'test@example.com',
      password: 'Password123!',
      ageConfirmed: true,
    });

    component.onSubmit();

    expect(authService.login).toHaveBeenCalledWith(
      jasmine.objectContaining({
        email: 'test@example.com',
        password: 'Password123!',
        ageConfirmed: true,
      })
    );
  });

  it('redirects when authentication becomes true', () => {
    const navigateSpy = spyOn(router, 'navigate').and.returnValue(Promise.resolve(true));

    isAuthenticatedSubject.next(true);

    expect(navigateSpy).toHaveBeenCalledWith([component.returnUrl]);
  });
});

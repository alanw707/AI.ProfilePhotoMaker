import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { of } from 'rxjs';

import { VerifyEmailComponent } from './verify-email.component';
import { AuthService } from '../../services/auth.service';
import { IntentTrackingService, SignupIntent } from '../../services/intent-tracking.service';

describe('VerifyEmailComponent', () => {
  let mockAuthService: jasmine.SpyObj<AuthService>;
  let mockRouter: jasmine.SpyObj<Router>;
  let mockIntentTracking: jasmine.SpyObj<IntentTrackingService>;

  const headshotsIntent: SignupIntent = {
    sourcePage: 'linkedin-headshots',
    ctaType: 'headshots',
    timestamp: Date.now(),
  };

  beforeEach(async () => {
    mockAuthService = jasmine.createSpyObj('AuthService', [
      'isAuthenticated',
      'getAccountStatus',
      'resendConfirmationEmail',
      'devConfirmEmail',
      'clearAuthCache',
      'logout',
    ]);
    mockRouter = jasmine.createSpyObj('Router', ['navigate', 'navigateByUrl']);
    mockIntentTracking = jasmine.createSpyObj('IntentTrackingService', ['getIntent']);

    mockAuthService.isAuthenticated.and.returnValue(true);
    mockAuthService.getAccountStatus.and.returnValue(
      of({ success: true, data: { emailConfirmed: false } }) as any
    );
    mockIntentTracking.getIntent.and.returnValue(headshotsIntent);

    await TestBed.configureTestingModule({
      imports: [VerifyEmailComponent],
      providers: [
        { provide: AuthService, useValue: mockAuthService },
        { provide: Router, useValue: mockRouter },
        { provide: IntentTrackingService, useValue: mockIntentTracking },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              queryParamMap: {
                get: () => null,
              },
            },
          },
        },
      ],
    }).compileComponents();
  });

  it('loads signup intent on init', () => {
    const fixture = TestBed.createComponent(VerifyEmailComponent);
    const component = fixture.componentInstance;

    component.ngOnInit();

    expect(component.signupIntent?.ctaType).toBe('headshots');
  });

  it('returns headshots urgency copy', () => {
    const fixture = TestBed.createComponent(VerifyEmailComponent);
    const component = fixture.componentInstance;
    component.signupIntent = headshotsIntent;

    expect(component.getUrgencyMessage()).toContain('LinkedIn-ready headshots');
  });

  it('returns source-specific preview images for medical intent', () => {
    const fixture = TestBed.createComponent(VerifyEmailComponent);
    const component = fixture.componentInstance;
    component.signupIntent = {
      sourcePage: 'doctor-headshots',
      ctaType: 'headshots',
      timestamp: Date.now(),
    };

    const previews = component.getPreviewImages();

    expect(previews[0].src).toContain('/medical-before.jpg');
    expect(previews[1].src).toContain('/medical.jpg');
  });

  it('returns progress label', () => {
    const fixture = TestBed.createComponent(VerifyEmailComponent);
    const component = fixture.componentInstance;

    expect(component.getProgressStep()).toBe('Step 2 of 3: Verify Email');
  });
});

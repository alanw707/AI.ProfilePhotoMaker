import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { RouterTestingModule } from '@angular/router/testing';

import { RegisterComponent } from './register.component';
import { AuthService } from '../../services/auth.service';
import { ConfigService } from '../../services/config.service';
import { IntentTrackingService } from '../../services/intent-tracking.service';

describe('RegisterComponent intent parsing', () => {
  const authService = jasmine.createSpyObj('AuthService', ['register']);
  const intentTracking = jasmine.createSpyObj('IntentTrackingService', [
    'isValidIntent',
    'storeIntent',
  ]);

  const configure = async (intentParam: string | null) => {
    await TestBed.configureTestingModule({
      imports: [RegisterComponent, RouterTestingModule],
      providers: [
        { provide: AuthService, useValue: authService },
        { provide: IntentTrackingService, useValue: intentTracking },
        {
          provide: ConfigService,
          useValue: {
            turnstileSiteKey: '',
            frontendBaseUrl: 'http://localhost:4200',
            getOAuthBaseUrl: () => 'http://localhost:5000',
          },
        },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              queryParamMap: {
                get: (_key: string) => intentParam,
              },
            },
          },
        },
      ],
    }).compileComponents();
  };

  beforeEach(() => {
    authService.register.calls.reset();
    intentTracking.isValidIntent.calls.reset();
    intentTracking.storeIntent.calls.reset();
  });

  it('parses encoded intent payloads and stores valid intent', async () => {
    const encodedIntent = encodeURIComponent(
      JSON.stringify({
        sourcePage: 'linkedin-headshots',
        ctaType: 'headshots',
        timestamp: Date.now(),
      })
    );

    intentTracking.isValidIntent.and.returnValue(true);
    await configure(encodedIntent);

    const fixture = TestBed.createComponent(RegisterComponent);
    fixture.componentInstance.ngOnInit();

    expect(intentTracking.storeIntent).toHaveBeenCalled();
  });

  it('logs warning and continues when intent query param is malformed', async () => {
    const warnSpy = spyOn(console, 'warn');
    await configure('{bad-json');

    const fixture = TestBed.createComponent(RegisterComponent);
    fixture.componentInstance.ngOnInit();

    expect(warnSpy).toHaveBeenCalledWith('Failed to parse signup intent query param.');
    expect(intentTracking.storeIntent).not.toHaveBeenCalled();
  });

  it('links the age checkbox to its error for assistive tech', async () => {
    await configure(null);
    const fixture = TestBed.createComponent(RegisterComponent);
    fixture.detectChanges();
    const box = fixture.nativeElement.querySelector('input[formcontrolname=ageConfirmed]') as HTMLInputElement;
    expect(box.getAttribute('aria-invalid')).toBeNull();
    fixture.componentInstance.f['ageConfirmed'].markAsTouched();
    fixture.detectChanges();
    expect(box.getAttribute('aria-invalid')).toBe('true');
    const id = box.getAttribute('aria-describedby');
    const err = id ? fixture.nativeElement.querySelector('#' + id) : null;
    expect(err?.textContent).toContain('at least 13');
    expect(err?.getAttribute('role')).toBe('alert');
  });
});

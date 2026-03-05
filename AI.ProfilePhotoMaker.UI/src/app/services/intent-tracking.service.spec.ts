import { TestBed } from '@angular/core/testing';

import { IntentTrackingService, SignupIntent } from './intent-tracking.service';

describe('IntentTrackingService', () => {
  let service: IntentTrackingService;

  const makeIntent = (overrides: Partial<SignupIntent> = {}): SignupIntent => ({
    sourcePage: 'linkedin-headshots',
    ctaType: 'headshots',
    timestamp: Date.now(),
    ...overrides,
  });

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(IntentTrackingService);
    sessionStorage.clear();
  });

  afterEach(() => {
    sessionStorage.clear();
  });

  it('stores and retrieves a valid intent', () => {
    const intent = makeIntent();

    service.storeIntent(intent);

    expect(service.getIntent()).toEqual(intent);
  });

  it('clears intent when requested', () => {
    service.storeIntent(makeIntent());

    service.clearIntent();

    expect(service.getIntent()).toBeNull();
  });

  it('returns null and clears malformed JSON', () => {
    sessionStorage.setItem('signupIntent', '{bad-json');

    expect(service.getIntent()).toBeNull();
    expect(sessionStorage.getItem('signupIntent')).toBeNull();
  });

  it('returns null for expired intent and clears storage', () => {
    const eightDaysMs = 8 * 24 * 60 * 60 * 1000;
    service.storeIntent(makeIntent({ timestamp: Date.now() - eightDaysMs }));

    expect(service.getIntent()).toBeNull();
    expect(sessionStorage.getItem('signupIntent')).toBeNull();
  });

  it('validates headshots helper', () => {
    service.storeIntent(makeIntent({ ctaType: 'headshots' }));
    expect(service.hasHeadshotsIntent()).toBeTrue();

    service.storeIntent(makeIntent({ ctaType: 'pricing' }));
    expect(service.hasHeadshotsIntent()).toBeFalse();
  });
});

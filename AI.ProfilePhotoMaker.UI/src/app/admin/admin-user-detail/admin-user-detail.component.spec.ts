import { ChangeDetectorRef, NgZone } from '@angular/core';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { Subject, throwError } from 'rxjs';
import { AdminService, AdminUserDiagnosticsDto } from '../../services/admin.service';
import { AdminUserDetailComponent } from './admin-user-detail.component';

describe('AdminUserDetailComponent', () => {
  let component: AdminUserDetailComponent;
  let adminService: jasmine.SpyObj<AdminService>;
  let paramMap$: Subject<ReturnType<typeof convertToParamMap>>;

  const diagnosticsResponse: AdminUserDiagnosticsDto = {
    user: {
      id: 'user-1',
      email: 'user@example.com',
      firstName: 'Ada',
      lastName: 'Lovelace',
      credits: 0,
      isLockedOut: false,
      createdAt: '2026-03-01T00:00:00Z',
      lastLoginAt: null,
      gender: null,
      ethnicity: null,
      subscriptionTier: 'Basic',
      emailConfirmed: true,
      roles: ['User'],
    },
    metrics: {
      currentCredits: 0,
      totalProcessedImages: 3,
      originalUploads: 1,
      generatedImages: 2,
      purchaseCount: 1,
      totalCreditsPurchased: 20,
      totalCreditsConsumed: 20,
      lastPurchaseAt: '2026-03-02T00:00:00Z',
      lastImageUploadAt: '2026-03-03T00:00:00Z',
      lastImageGenerationAt: '2026-03-04T00:00:00Z',
      lastActivityAt: '2026-03-05T00:00:00Z',
      hasUsageHistory: true,
    },
    recentPurchases: [],
    packageEntitlements: [],
    recentImages: [],
    activityHistory: [],
    recentAdminActions: [],
  };

  beforeEach(() => {
    adminService = jasmine.createSpyObj<AdminService>('AdminService', ['getUserDetail']);
    paramMap$ = new Subject<ReturnType<typeof convertToParamMap>>();

    const route = {
      paramMap: paramMap$.asObservable(),
      snapshot: {
        paramMap: convertToParamMap({ userId: 'user-1' }),
      },
    } as ActivatedRoute;

    const cdr = {
      detectChanges: () => {},
    } as ChangeDetectorRef;

    const ngZone = {
      run: (fn: () => void) => fn(),
    } as NgZone;

    component = new AdminUserDetailComponent(route, adminService, cdr, ngZone);
  });

  afterEach(() => {
    component.ngOnDestroy();
    paramMap$.complete();
  });

  it('loads diagnostics on init', () => {
    adminService.getUserDetail.and.returnValue(new Subject<AdminUserDiagnosticsDto>());

    component.ngOnInit();
    paramMap$.next(convertToParamMap({ userId: 'user-1' }));

    expect(adminService.getUserDetail).toHaveBeenCalledWith('user-1');
    expect(component.isLoading).toBeTrue();
  });

  it('stores diagnostics when the request succeeds', () => {
    const response$ = new Subject<AdminUserDiagnosticsDto>();
    adminService.getUserDetail.and.returnValue(response$);

    component.ngOnInit();
    paramMap$.next(convertToParamMap({ userId: 'user-1' }));
    response$.next(diagnosticsResponse);
    response$.complete();

    expect(component.diagnostics).toEqual(diagnosticsResponse);
    expect(component.error).toBeNull();
    expect(component.isLoading).toBeFalse();
    expect(component.hasDiagnostics).toBeTrue();
  });

  it('shows an error when the diagnostics request fails', () => {
    adminService.getUserDetail.and.returnValue(throwError(() => ({ message: 'Request failed' })));

    component.ngOnInit();
    paramMap$.next(convertToParamMap({ userId: 'user-1' }));

    expect(component.error).toBe('Request failed');
    expect(component.diagnostics).toBeNull();
    expect(component.isLoading).toBeFalse();
  });

  it('shows an explicit error when the route does not include a user id', () => {
    component.ngOnInit();
    paramMap$.next(convertToParamMap({}));

    expect(adminService.getUserDetail).not.toHaveBeenCalled();
    expect(component.error).toContain('No user was selected');
    expect(component.hasDiagnostics).toBeFalse();
  });
});

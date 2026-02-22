import { ChangeDetectorRef, NgZone } from '@angular/core';
import { NavigationStart, Router } from '@angular/router';
import { NEVER, Subject, of, throwError } from 'rxjs';
import { AdminService, AdminDashboardDto } from '../../services/admin.service';
import { CookieConsentService } from '../../services/cookie-consent.service';
import { AdminDashboardComponent } from './admin-dashboard.component';

describe('AdminDashboardComponent', () => {
  let component: AdminDashboardComponent;
  let adminService: jasmine.SpyObj<AdminService>;
  let routerEvents$: Subject<unknown>;

  const dashboardResponse: AdminDashboardDto = {
    totalUsers: 12,
    activeUsers: 9,
    totalCreditsOutstanding: 500,
    totalCreditsPurchased: 1200,
    activeCoupons: 4,
  };

  beforeEach(() => {
    adminService = jasmine.createSpyObj<AdminService>('AdminService', ['getDashboard']);
    routerEvents$ = new Subject<unknown>();
    const router = { events: routerEvents$.asObservable() } as Router;

    const cdr = {
      detectChanges: () => {},
    } as ChangeDetectorRef;

    const ngZone = {
      run: (fn: () => void) => fn(),
    } as NgZone;

    const cookieConsentService = {
      consent$: of(null),
    } as CookieConsentService;

    component = new AdminDashboardComponent(
      adminService,
      router,
      cdr,
      ngZone,
      cookieConsentService
    );
  });

  afterEach(() => {
    component.ngOnDestroy();
    routerEvents$.complete();
  });

  it('loads dashboard statistics on init', () => {
    adminService.getDashboard.and.returnValue(of(dashboardResponse));

    component.ngOnInit();

    expect(adminService.getDashboard).toHaveBeenCalledTimes(1);
    expect(component.dashboard).toEqual(dashboardResponse);
    expect(component.error).toBeNull();
    expect(component.isLoading).toBeFalse();
  });

  it('cancels an in-flight load when navigating away from dashboard', () => {
    adminService.getDashboard.and.returnValue(NEVER);

    component.ngOnInit();
    expect(component.isLoading).toBeTrue();

    routerEvents$.next(new NavigationStart(1, '/admin/users'));

    expect(component.isLoading).toBeFalse();
  });

  it('keeps loading when navigation stays on dashboard route', () => {
    adminService.getDashboard.and.returnValue(NEVER);

    component.ngOnInit();
    expect(component.isLoading).toBeTrue();

    routerEvents$.next(new NavigationStart(1, '/admin/dashboard?refresh=1'));

    expect(component.isLoading).toBeTrue();
  });

  it('shows an error message when dashboard request fails', () => {
    spyOn(console, 'error');
    adminService.getDashboard.and.returnValue(throwError(() => ({ message: 'Request failed' })));

    component.loadDashboard();

    expect(component.error).toBe('Request failed');
    expect(component.isLoading).toBeFalse();
  });
});

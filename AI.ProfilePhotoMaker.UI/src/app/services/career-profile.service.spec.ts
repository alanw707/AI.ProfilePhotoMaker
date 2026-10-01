import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CareerProfileService, CareerApiError } from './career-profile.service';

describe('CareerProfileService', () => {
  let service: CareerProfileService;
  let http: HttpTestingController;
  const facts = { currentTitle: 'Analyst', skills: [], highlights: [], confirmed: true };
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(CareerProfileService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  it('creates without If-Match and updates with the response ETag', () => {
    service.saveProfile(facts).subscribe();
    const create = http.expectOne('/api/career/profile');
    expect(create.request.headers.has('If-Match')).toBeFalse();
    create.flush(
      { success: true, data: { etag: '"profile-v1"', facts } },
      { headers: { ETag: '"profile-v1"' } }
    );
    service.saveProfile(facts).subscribe();
    const update = http.expectOne('/api/career/profile');
    expect(update.request.headers.get('If-Match')).toBe('"profile-v1"');
    update.flush({ success: true, data: { etag: '"profile-v2"', facts } });
  });
  it('restores a goal version with the current goal ETag', () => {
    service.getGoal().subscribe();
    http
      .expectOne('/api/career/goals')
      .flush(
        { success: true, data: { id: 'g1', etag: '"goal-v2"' } },
        { headers: { ETag: '"goal-v2"' } }
      );
    service.restoreGoal('g1', 1).subscribe(goal => expect(goal.version).toBe(3));
    const restore = http.expectOne('/api/career/goals/g1/versions/1/restore');
    expect(restore.request.method).toBe('POST');
    expect(restore.request.headers.get('If-Match')).toBe('"goal-v2"');
    restore.flush({ success: true, data: { id: 'g1', version: 3, etag: '"goal-v3"' } });
  });
  it('maps stale ETags to conflict', () => {
    service
      .saveProfile(facts)
      .subscribe({ error: (error: CareerApiError) => expect(error.kind).toBe('conflict') });
    http
      .expectOne('/api/career/profile')
      .flush(
        { success: false, error: { code: 'CareerVersionConflict' } },
        { status: 412, statusText: 'Precondition Failed' }
      );
  });
  it('maps field errors and disabled flag', () => {
    service.saveProfile(facts).subscribe({
      error: (error: CareerApiError) => {
        expect(error.kind).toBe('validation');
        expect(error.fieldErrors?.['currentTitle']).toBe('Required.');
      },
    });
    http.expectOne('/api/career/profile').flush(
      {
        success: false,
        error: { code: 'ValidationError', fieldErrors: { currentTitle: 'Required.' } },
      },
      { status: 400, statusText: 'Bad Request' }
    );
    service
      .getProfile()
      .subscribe({ error: (error: CareerApiError) => expect(error.kind).toBe('disabled') });
    http
      .expectOne('/api/career/profile')
      .flush(
        { success: false, error: { code: 'CareerWorkspaceDisabled' } },
        { status: 403, statusText: 'Forbidden' }
      );
  });
});

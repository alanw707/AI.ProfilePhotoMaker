import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import {
  RESUME_CONSENT_VERSION,
  CareerProfileService,
  CareerApiError,
} from './career-profile.service';

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

  describe('resume import', () => {
    const fail = (status: number, body: object, headers: Record<string, string> = {}) =>
      http
        .expectOne('/api/career/resumes')
        .flush({ success: false, error: body }, { status, statusText: 'Error', headers });
    const upload = () => {
      let result: CareerApiError | undefined;
      service
        .uploadResume(new File(['x'], 'r.pdf'), true)
        .subscribe({ error: (e: CareerApiError) => (result = e) });
      return () => result;
    };
    it('uploads multipart with file and consent fields', () => {
      service.uploadResume(new File(['%PDF-'], 'resume.pdf'), true).subscribe();
      const req = http.expectOne('/api/career/resumes');
      expect(req.request.method).toBe('POST');
      const form = req.request.body as FormData;
      expect(form instanceof FormData).toBeTrue();
      expect((form.get('file') as File).name).toBe('resume.pdf');
      expect(form.get('consent')).toBe('true');
      expect(form.get('consentVersion')).toBe(RESUME_CONSENT_VERSION);
      req.flush({ success: true, data: { id: 'r1', state: 'ready' } });
    });
    it('maps 413 to tooLarge', () => {
      const result = upload();
      fail(413, { code: 'CareerResumeTooLarge', message: 'big' });
      expect(result()?.kind).toBe('tooLarge');
    });
    it('maps 415 to unsupported with detail', () => {
      const result = upload();
      fail(415, { code: 'CareerResumeUnsupported', message: 'no', detail: 'encrypted PDF' });
      expect(result()?.kind).toBe('unsupported');
      expect(result()?.detail).toBe('encrypted PDF');
    });
    it('maps 422 to rejected', () => {
      const result = upload();
      fail(422, { code: 'CareerResumeRejected' });
      expect(result()?.kind).toBe('rejected');
    });
    it('maps 503 to scannerUnavailable with retryAfterSeconds', () => {
      const result = upload();
      fail(503, { code: 'CareerScannerUnavailable', retryAfterSeconds: 60 });
      expect(result()?.kind).toBe('scannerUnavailable');
      expect(result()?.retryAfterSeconds).toBe(60);
    });
    it('falls back to the Retry-After header', () => {
      const result = upload();
      fail(503, { code: 'CareerScannerUnavailable' }, { 'Retry-After': '30' });
      expect(result()?.retryAfterSeconds).toBe(30);
    });
    it('lists, gets and deletes resumes', () => {
      service.listResumes().subscribe(list => expect(list.length).toBe(1));
      http.expectOne('/api/career/resumes').flush({ success: true, data: [{ id: 'r1' }] });
      service.getResume('r1').subscribe();
      http.expectOne('/api/career/resumes/r1').flush({ success: true, data: { id: 'r1' } });
      service.deleteResume('r1').subscribe();
      const del = http.expectOne('/api/career/resumes/r1');
      expect(del.request.method).toBe('DELETE');
      del.flush(null, { status: 204, statusText: 'No Content' });
    });
    it('downloads the original file as a blob', () => {
      service.downloadResume('r1').subscribe(blob => expect(blob.size).toBe(3));
      const req = http.expectOne('/api/career/resumes/r1/file');
      expect(req.request.responseType).toBe('blob');
      req.flush(new Blob(['abc']));
    });
    it('posts pasted text and fetches/dismisses proposals', () => {
      service.createPastedProposal('Jane Doe').subscribe();
      const paste = http.expectOne('/api/career/profile/proposals');
      expect(paste.request.body).toEqual({ text: 'Jane Doe' });
      paste.flush({ success: true, data: { id: 'p1' } });
      service.getProposal('p1').subscribe();
      http.expectOne('/api/career/profile/proposals/p1').flush({ success: true, data: {} });
      service.dismissProposal('p1').subscribe();
      const dismiss = http.expectOne('/api/career/profile/proposals/p1/dismiss');
      expect(dismiss.request.method).toBe('POST');
      dismiss.flush({ success: true, data: {} });
    });
    it('accepts with the profile If-Match and stores the new ETag', () => {
      service.getProfile().subscribe();
      http
        .expectOne('/api/career/profile')
        .flush(
          { success: true, data: { etag: '"profile-v1"' } },
          { headers: { ETag: '"profile-v1"' } }
        );
      service.acceptProposal('p1', ['i1', 'i2']).subscribe();
      const accept = http.expectOne('/api/career/profile/proposals/p1/accept');
      expect(accept.request.headers.get('If-Match')).toBe('"profile-v1"');
      expect(accept.request.body).toEqual({ itemIds: ['i1', 'i2'] });
      accept.flush(
        { success: true, data: { etag: '"profile-v2"' } },
        { headers: { ETag: '"profile-v2"' } }
      );
      service.saveProfile(facts).subscribe();
      const next = http.expectOne('/api/career/profile');
      expect(next.request.headers.get('If-Match')).toBe('"profile-v2"');
      next.flush({ success: true, data: { etag: '"profile-v3"' } });
    });
    it('maps stale accept to conflict', () => {
      let kind = '';
      service
        .acceptProposal('p1', ['i1'])
        .subscribe({ error: (e: CareerApiError) => (kind = e.kind) });
      http
        .expectOne('/api/career/profile/proposals/p1/accept')
        .flush({ success: false, error: {} }, { status: 412, statusText: 'Precondition Failed' });
      expect(kind).toBe('conflict');
    });
  });

  describe('photos', () => {
    const failWith = (status: number, code: string) => ({
      status,
      statusText: code,
      body: { success: false, error: { code, message: code } },
    });

    it('lists photos without sending a body', () => {
      service.listPhotos().subscribe(list => expect(list.selectedPhotoId).toBe(42));
      const req = http.expectOne('/api/career/photos');
      expect(req.request.method).toBe('GET');
      req.flush({
        success: true,
        data: { photos: [], selectedPhotoId: 42, selectedPhotoAvailable: true, entitlements: [] },
      });
    });

    it('selects a photo with the processed image id', () => {
      service.selectPhoto(42).subscribe(result => expect(result.selectedPhotoId).toBe(42));
      const req = http.expectOne('/api/career/photos/selection');
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({ processedImageId: 42 });
      req.flush({
        success: true,
        data: { selectedPhotoId: 42, careerGoalId: null, selectedAt: '2026-10-04T00:00:00Z' },
      });
    });

    it('clears the selection with DELETE', () => {
      let done = false;
      service.clearPhoto().subscribe(() => (done = true));
      const req = http.expectOne('/api/career/photos/selection');
      expect(req.request.method).toBe('DELETE');
      req.flush(null, { status: 204, statusText: 'No Content' });
      expect(done).toBeTrue();
    });

    const cases: [number, string, CareerApiError['kind']][] = [
      [404, 'CareerPhotoNotFound', 'notFound'],
      [409, 'CareerPhotoIsPreview', 'preview'],
      [401, 'Unauthorized', 'unauthorized'],
      [403, 'CareerWorkspaceDisabled', 'disabled'],
      [400, 'ValidationError', 'validation'],
    ];
    for (const [status, code, kind] of cases) {
      it(`maps ${status} ${code} to ${kind} when selecting`, () => {
        let seen: CareerApiError | undefined;
        service.selectPhoto(7).subscribe({ error: (e: CareerApiError) => (seen = e) });
        const failure = failWith(status, code);
        http
          .expectOne('/api/career/photos/selection')
          .flush(failure.body, { status, statusText: failure.statusText });
        expect(seen?.kind).toBe(kind);
      });
    }

    it('maps 401 when listing and when clearing', () => {
      const kinds: string[] = [];
      service.listPhotos().subscribe({ error: (e: CareerApiError) => kinds.push(e.kind) });
      http
        .expectOne('/api/career/photos')
        .flush({ success: false }, { status: 401, statusText: 'Unauthorized' });
      service.clearPhoto().subscribe({ error: (e: CareerApiError) => kinds.push(e.kind) });
      http
        .expectOne('/api/career/photos/selection')
        .flush({ success: false }, { status: 401, statusText: 'Unauthorized' });
      expect(kinds).toEqual(['unauthorized', 'unauthorized']);
    });
  });
  describe('agent runs', () => {
    const run = { id: 'r1', status: 'queued', steps: [] };
    const err = (status: number, code: string) => ({
      status,
      body: { success: false, error: { code, message: code } },
    });
    it('creates a run with the Idempotency-Key header and task body', () => {
      service.createRun('key-1').subscribe(r => expect(r.id).toBe('r1'));
      const req = http.expectOne('/api/career/runs');
      expect(req.request.method).toBe('POST');
      expect(req.request.headers.get('Idempotency-Key')).toBe('key-1');
      expect(req.request.body).toEqual({ task: 'profile_summary' });
      req.flush({ success: true, data: run });
    });
    it('reads, lists, answers and cancels runs', () => {
      service.getRun('r 1').subscribe();
      http.expectOne('/api/career/runs/r%201').flush({ success: true, data: run });
      service.listRuns().subscribe(l => expect(l.runs.length).toBe(1));
      http.expectOne('/api/career/runs').flush({ success: true, data: { runs: [run] } });
      service.answerRun('r1', 'audience', 'Recruiters').subscribe();
      const answer = http.expectOne('/api/career/runs/r1/answers');
      expect(answer.request.body).toEqual({ questionId: 'audience', answer: 'Recruiters' });
      answer.flush({ success: true, data: run });
      service.cancelRun('r1').subscribe();
      const cancel = http.expectOne('/api/career/runs/r1/cancel');
      expect(cancel.request.method).toBe('POST');
      cancel.flush({ success: true, data: run });
    });
    const cases: [number, string, string][] = [
      [429, 'CareerAllowanceExhausted', 'allowance'],
      [503, 'CareerModelUnavailable', 'unavailable'],
      [409, 'CareerIdempotencyMismatch', 'idempotencyMismatch'],
      [409, 'CareerProfileRequired', 'profileRequired'],
      [409, 'CareerRunNotWaiting', 'notWaiting'],
      [404, 'CareerRunNotFound', 'notFound'],
      [401, 'Unauthorized', 'unauthorized'],
      [403, 'CareerWorkspaceDisabled', 'disabled'],
      [409, 'CareerMatchStale', 'matchStale'],
      [409, 'CareerMatchNotConfirmable', 'notConfirmable'],
      [409, 'CareerGoalRequired', 'goalRequired'],
      [412, 'CareerVersionConflict', 'conflict'],
      [503, 'CareerReferenceUnavailable', 'unavailable'],
    ];
    for (const [status, code, kind] of cases) {
      it(`maps ${status} ${code} to ${kind}`, () => {
        let caught: CareerApiError | undefined;
        service.createRun('k').subscribe({ error: e => (caught = e) });
        const e = err(status, code);
        http.expectOne('/api/career/runs').flush(e.body, { status, statusText: code });
        expect(caught?.kind).toBe(kind as CareerApiError['kind']);
        expect(caught?.code).toBe(code);
      });
    }
  });
  describe('occupation matches', () => {
    it('starts an occupation_match run', () => {
      service.createRun('k', 'occupation_match').subscribe();
      const req = http.expectOne('/api/career/runs');
      expect(req.request.body).toEqual({ task: 'occupation_match' });
      req.flush({ success: true, data: { id: 'r1' } });
    });
    it('reads a match and the reference', () => {
      service.getOccupationMatch('m 1').subscribe(m => expect(m.id).toBe('m 1'));
      http
        .expectOne('/api/career/occupation-matches/m%201')
        .flush({ success: true, data: { id: 'm 1' } });
      service.getOccupationReference().subscribe(r => expect(r.occupationCount).toBe(900));
      http
        .expectOne('/api/career/occupations/reference')
        .flush({ success: true, data: { occupationCount: 900 } });
    });
    it('confirms with the given If-Match and a code body', () => {
      service.confirmOccupationMatch('m1', '15-1252.00', '"goal-v2"').subscribe();
      const req = http.expectOne('/api/career/occupation-matches/m1/confirm');
      expect(req.request.method).toBe('POST');
      expect(req.request.headers.get('If-Match')).toBe('"goal-v2"');
      expect(req.request.body).toEqual({ occupationCode: '15-1252.00' });
      req.flush({ success: true, data: { etag: '"goal-v3"' } }, { headers: { ETag: '"goal-v3"' } });
    });
    it('dismisses a match', () => {
      service.dismissOccupationMatch('m1').subscribe();
      const req = http.expectOne('/api/career/occupation-matches/m1/dismiss');
      expect(req.request.method).toBe('POST');
      req.flush({ success: true, data: { id: 'm1', status: 'dismissed' } });
    });
    it('maps confirm failures', () => {
      const kinds: string[] = [];
      for (const [status, code] of [
        [412, 'CareerVersionConflict'],
        [409, 'CareerMatchStale'],
      ] as const) {
        service
          .confirmOccupationMatch('m1', 'x', '"g"')
          .subscribe({ error: e => kinds.push(e.kind) });
        http
          .expectOne('/api/career/occupation-matches/m1/confirm')
          .flush({ success: false, error: { code } }, { status, statusText: code });
      }
      expect(kinds).toEqual(['conflict', 'matchStale']);
    });
  });
});

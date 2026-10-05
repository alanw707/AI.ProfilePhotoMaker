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
      [409, 'CareerOccupationRequired', 'occupationRequired'],
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
  describe('pay analyses', () => {
    it('starts a pay_analysis run with a key and reads its result id', () => {
      service
        .createRun('pay-key', 'pay_analysis')
        .subscribe(r => expect(r.payAnalysisId).toBe('a1'));
      const req = http.expectOne('/api/career/runs');
      expect(req.request.method).toBe('POST');
      expect(req.request.headers.get('Idempotency-Key')).toBe('pay-key');
      expect(req.request.body).toEqual({ task: 'pay_analysis' });
      req.flush({ success: true, data: { id: 'r1', payAnalysisId: 'a1' } });
    });
    it('lists, reads, recomputes and gets qualification', () => {
      service.listPayAnalyses().subscribe(r => expect(r.analyses[0].id).toBe('a1'));
      http
        .expectOne('/api/career/pay-analyses')
        .flush({ success: true, data: { analyses: [{ id: 'a1' }] } });
      service.getPayAnalysis('a 1').subscribe(r => expect(r.inputHash).toBe('hash'));
      http
        .expectOne('/api/career/pay-analyses/a%201')
        .flush({ success: true, data: { inputHash: 'hash' } });
      service.recomputePayAnalysis('a 1').subscribe(r => expect(r.matches).toBeTrue());
      const recompute = http.expectOne('/api/career/pay-analyses/a%201/recompute');
      expect(recompute.request.method).toBe('POST');
      recompute.flush({ success: true, data: { matches: true, differences: [] } });
      service.getPayQualification().subscribe(r => expect(r.personalizedAllowed).toBeFalse());
      http
        .expectOne('/api/career/pay/qualification')
        .flush({ success: true, data: { personalizedAllowed: false, gates: [] } });
    });
    it('maps pay errors through the shared error mapper', () => {
      const kinds: string[] = [];
      service.getPayAnalysis('x').subscribe({ error: (e: CareerApiError) => kinds.push(e.kind) });
      http
        .expectOne('/api/career/pay-analyses/x')
        .flush(
          { success: false, error: { code: 'CareerPayAnalysisNotFound' } },
          { status: 404, statusText: 'Not Found' }
        );
      service.getPayQualification().subscribe({ error: (e: CareerApiError) => kinds.push(e.kind) });
      http
        .expectOne('/api/career/pay/qualification')
        .flush(
          { success: false, error: { code: 'CareerWorkspaceDisabled' } },
          { status: 403, statusText: 'Forbidden' }
        );
      expect(kinds).toEqual(['notFound', 'disabled']);
    });
  });
  describe('market briefs', () => {
    it('starts a market_brief run', () => {
      service.createRun('k', 'market_brief').subscribe();
      const req = http.expectOne('/api/career/runs');
      expect(req.request.body).toEqual({ task: 'market_brief' });
      expect(req.request.headers.get('Idempotency-Key')).toBe('k');
      req.flush({ success: true, data: { id: 'r1', marketBriefId: 'b1' } });
    });
    it('lists and reads briefs and the reference', () => {
      service.listMarketBriefs().subscribe(r => expect(r.briefs.length).toBe(1));
      http
        .expectOne('/api/career/market-briefs')
        .flush({ success: true, data: { briefs: [{ id: 'b1' }] } });
      service.getMarketBrief('b 1').subscribe(b => expect(b.id).toBe('b 1'));
      http
        .expectOne('/api/career/market-briefs/b%201')
        .flush({ success: true, data: { id: 'b 1' } });
      service.getMarketReference().subscribe(r => expect(r.areaCount).toBe(445));
      http
        .expectOne('/api/career/market/reference')
        .flush({ success: true, data: { sources: [], areaCount: 445, occupationCount: 831 } });
    });
    it('maps a missing brief and an unavailable reference', () => {
      const kinds: string[] = [];
      service.getMarketBrief('x').subscribe({ error: e => kinds.push(e.kind) });
      http
        .expectOne('/api/career/market-briefs/x')
        .flush(
          { success: false, error: { code: 'CareerMarketBriefNotFound', message: 'm' } },
          { status: 404, statusText: 'x' }
        );
      service.getMarketReference().subscribe({ error: e => kinds.push(e.kind) });
      http
        .expectOne('/api/career/market/reference')
        .flush(
          { success: false, error: { code: 'CareerReferenceUnavailable', message: 'm' } },
          { status: 503, statusText: 'x' }
        );
      expect(kinds).toEqual(['notFound', 'unavailable']);
    });
  });

  describe('market comparison', () => {
    it('reads metrics and compares with a bounded query string', () => {
      service.getMarketMetrics().subscribe(r => expect(r.metrics.length).toBe(1));
      http
        .expectOne('/api/career/markets/metrics')
        .flush({ success: true, data: { metrics: [{}] } });
      service
        .compareMarkets({ metric: 'median_wage', level: 'state', areas: ['08', '06'], q: 'co l' })
        .subscribe(r => expect(r.level).toBe('state'));
      http
        .expectOne(
          '/api/career/markets/compare?metric=median_wage&level=state&areas=08%2C06&q=co+l'
        )
        .flush({ success: true, data: { level: 'state', areas: [] } });
      service.compareMarkets({ metric: 'employment', level: 'metro' }).subscribe();
      http
        .expectOne('/api/career/markets/compare?metric=employment&level=metro')
        .flush({ success: true, data: {} });
    });
    it('saves a preference with the explicit ETag and confirmation', () => {
      service.saveMarketPreference({ areaCode: '19740', level: 'metro' }, '"goal-v2"').subscribe();
      const req = http.expectOne('/api/career/markets/preference');
      expect(req.request.method).toBe('POST');
      expect(req.request.headers.get('If-Match')).toBe('"goal-v2"');
      expect(req.request.body).toEqual({ areaCode: '19740', level: 'metro', confirmed: true });
      req.flush({ success: true, data: { etag: '"goal-v3"' } });
    });
    it('maps comparison and preference errors', () => {
      const kinds: string[] = [];
      const fail = (status: number, code: string) => {
        service
          .compareMarkets({ metric: 'm', level: 'state' })
          .subscribe({ error: (e: CareerApiError) => kinds.push(e.kind) });
        http
          .expectOne(r => r.url.startsWith('/api/career/markets/compare'))
          .flush({ success: false, error: { code, message: 'm' } }, { status, statusText: 'x' });
      };
      fail(409, 'CareerMetricUnsupported');
      fail(409, 'CareerOccupationRequired');
      fail(404, 'CareerAreaNotFound');
      for (const status of [412, 428]) {
        service
          .saveMarketPreference({ areaCode: '08', level: 'state' }, '"e"')
          .subscribe({ error: (e: CareerApiError) => kinds.push(e.kind) });
        http
          .expectOne('/api/career/markets/preference')
          .flush({ success: false, error: { message: 'm' } }, { status, statusText: 'x' });
      }
      expect(kinds).toEqual([
        'metricUnsupported',
        'occupationRequired',
        'areaNotFound',
        'conflict',
        'precondition',
      ]);
    });
  });
  describe('job observations', () => {
    it('sends only the filters that are set', () => {
      service
        .getJobObservations({ area: 'Denver, CO', eligibleOnly: true, remote: 'unknown', q: 'it' })
        .subscribe(r => expect(r.truncated).toBeFalse());
      http
        .expectOne(
          '/api/career/jobs/observations?area=Denver%2C+CO&eligibleOnly=true&remote=unknown&q=it'
        )
        .flush({ success: true, data: { truncated: false } });
      service.getJobObservations({ remote: 'all', eligibleOnly: false }).subscribe();
      http.expectOne('/api/career/jobs/observations').flush({ success: true, data: {} });
    });
    it('reads the source reference', () => {
      service.getJobSource().subscribe(r => expect(r.name).toBe('USAJOBS'));
      http.expectOne('/api/career/jobs/source').flush({ success: true, data: { name: 'USAJOBS' } });
    });
    it('maps errors like the other career reads', () => {
      const kinds: string[] = [];
      for (const [status, code] of [
        [403, 'CareerWorkspaceDisabled'],
        [401, ''],
        [409, 'CareerOccupationRequired'],
        [503, 'X'],
      ] as const) {
        service
          .getJobObservations()
          .subscribe({ error: (e: CareerApiError) => kinds.push(e.kind) });
        http
          .expectOne('/api/career/jobs/observations')
          .flush({ success: false, error: { code, message: 'm' } }, { status, statusText: 'x' });
      }
      expect(kinds).toEqual([
        'disabled',
        'unauthorized',
        'occupationRequired',
        'scannerUnavailable',
      ]);
    });
  });
  it('starts a roadmap run and reads the roadmap id', () => {
    service.createRun('k1', 'roadmap').subscribe(run => expect(run.roadmapId).toBe('r1'));
    const req = http.expectOne('/api/career/runs');
    expect(req.request.body).toEqual({ task: 'roadmap' });
    req.flush({ success: true, data: { id: 'run', roadmapId: 'r1' } });
  });
  it('accepts with the goal ETag, edits effort and dismisses', () => {
    service.acceptRoadmap('r1', 'closest_fit', '"goal-v2"').subscribe();
    const accept = http.expectOne('/api/career/roadmaps/r1/accept');
    expect(accept.request.headers.get('If-Match')).toBe('"goal-v2"');
    expect(accept.request.body).toEqual({ optionKey: 'closest_fit' });
    accept.flush({ success: true, data: { id: 'r1' } });
    service.updateRoadmapTask('r1', 't1', 3).subscribe();
    const put = http.expectOne('/api/career/roadmaps/r1/tasks/t1');
    expect(put.request.method).toBe('PUT');
    expect(put.request.body).toEqual({ effortHours: 3 });
    put.flush({ success: true, data: { id: 'r1' } });
    service.dismissRoadmap('r1').subscribe();
    http.expectOne('/api/career/roadmaps/r1/dismiss').flush({ success: true, data: { id: 'r1' } });
    service.listRoadmaps().subscribe();
    http.expectOne('/api/career/roadmaps').flush({ success: true, data: { roadmaps: [] } });
  });
  it('maps roadmap conflicts and preconditions', () => {
    const kinds: string[] = [];
    const fail = (status: number, code?: string) => {
      service.dismissRoadmap('r1').subscribe({ error: (e: CareerApiError) => kinds.push(e.kind) });
      http
        .expectOne('/api/career/roadmaps/r1/dismiss')
        .flush({ success: false, error: { code } }, { status, statusText: 'x' });
    };
    fail(409, 'CareerRoadmapCycle');
    fail(409, 'CareerRoadmapNotProposed');
    fail(409, 'CareerOccupationRequired');
    fail(412);
    fail(428);
    expect(kinds).toEqual([
      'roadmapCycle',
      'roadmapNotProposed',
      'occupationRequired',
      'conflict',
      'precondition',
    ]);
  });
  it('reads and saves task progress with If-Match, adds tasks and replans', () => {
    service.getProgress('r1').subscribe();
    http
      .expectOne('/api/career/roadmaps/r1/progress')
      .flush({ success: true, data: { roadmapId: 'r1', version: 1, tasks: [] } });
    service.updateTaskProgress('r1', 't1', { status: 'done' }, 'task-v2').subscribe();
    const put = http.expectOne('/api/career/roadmaps/r1/progress/t1');
    expect(put.request.method).toBe('PUT');
    expect(put.request.headers.get('If-Match')).toBe('task-v2');
    expect(put.request.body).toEqual({ status: 'done' });
    put.flush({ success: true, data: { taskId: 't1' } });
    service
      .addHumanTask('r1', { title: 'Mine', effortHours: 2, milestoneDay: 30, dependsOn: ['t1'] })
      .subscribe();
    const add = http.expectOne('/api/career/roadmaps/r1/tasks');
    expect(add.request.method).toBe('POST');
    add.flush({ success: true, data: { taskId: 'h1' } });
    service.replan('r1').subscribe();
    http
      .expectOne('/api/career/roadmaps/r1/replan')
      .flush({ success: true, data: { id: 'p1', changes: [], preserved: [] } });
    service.getReplan('p1').subscribe();
    http.expectOne('/api/career/replans/p1').flush({ success: true, data: { id: 'p1' } });
    service.applyReplan('p1', ['c1']).subscribe();
    const apply = http.expectOne('/api/career/replans/p1/apply');
    expect(apply.request.body).toEqual({ acceptedChangeIds: ['c1'] });
    apply.flush({ success: true, data: { id: 'r1' } });
    service.rejectReplan('p1').subscribe();
    const reject = http.expectOne('/api/career/replans/p1/reject');
    expect(reject.request.method).toBe('POST');
    reject.flush({ success: true, data: {} });
  });
  it('maps tracking and replan errors', () => {
    const kinds: string[] = [];
    const fail = (status: number, code?: string) => {
      service.replan('r1').subscribe({ error: (e: CareerApiError) => kinds.push(e.kind) });
      http
        .expectOne('/api/career/roadmaps/r1/replan')
        .flush({ success: false, error: { code } }, { status, statusText: 'x' });
    };
    fail(409, 'CareerRoadmapNotAccepted');
    fail(409, 'CareerRoadmapCycle');
    fail(409, 'CareerReplanStale');
    fail(409, 'CareerReplanClosed');
    fail(412);
    fail(428);
    expect(kinds).toEqual([
      'roadmapNotAccepted',
      'roadmapCycle',
      'replanStale',
      'replanClosed',
      'conflict',
      'precondition',
    ]);
  });

  it('starts a targeted resume run with an optional material id', () => {
    service.createRun('k1', 'targeted_resume').subscribe();
    expect(http.expectOne('/api/career/runs').request.body).toEqual({ task: 'targeted_resume' });
    service
      .createRun('k2', 'targeted_resume', 'm1')
      .subscribe(r => expect(r.materialId).toBe('m1'));
    const req = http.expectOne('/api/career/runs');
    expect(req.request.body).toEqual({ task: 'targeted_resume', materialId: 'm1' });
    req.flush({ success: true, data: { id: 'run', materialId: 'm1' } });
  });
  it('saves, restores and applies resume changes with If-Match', () => {
    const body = {
      sections: [],
      contact: { name: true, email: true, phone: false, location: false, links: false },
    };
    service.saveResume('m1', body, '"material-v1"').subscribe();
    const put = http.expectOne('/api/career/materials/m1');
    expect(put.request.method).toBe('PUT');
    expect(put.request.headers.get('If-Match')).toBe('"material-v1"');
    put.flush({ success: true, data: { id: 'm1' } });
    service.restoreResumeVersion('m1', 2, '"material-v3"').subscribe();
    const restore = http.expectOne('/api/career/materials/m1/versions/2/restore');
    expect(restore.request.headers.get('If-Match')).toBe('"material-v3"');
    restore.flush({ success: true, data: { id: 'm1' } });
    service.applyResumeProposal('m1', 'p1', ['c1'], '"material-v3"').subscribe();
    const apply = http.expectOne('/api/career/materials/m1/proposals/p1/apply');
    expect(apply.request.body).toEqual({ acceptedChangeIds: ['c1'] });
    expect(apply.request.headers.get('If-Match')).toBe('"material-v3"');
    apply.flush({ success: true, data: { id: 'm1' } });
  });
  it('lists materials, versions and proposals', () => {
    service.listResumeMaterials().subscribe();
    http
      .expectOne('/api/career/materials?kind=resume')
      .flush({ success: true, data: { materials: [] } });
    service.listResumeVersions('m1').subscribe();
    http
      .expectOne('/api/career/materials/m1/versions?page=1')
      .flush({ success: true, data: { versions: [], total: 0 } });
    service.getResumeProposal('m1', 'p1').subscribe();
    http
      .expectOne('/api/career/materials/m1/proposals/p1')
      .flush({ success: true, data: { id: 'p1' } });
    service.rejectResumeProposal('m1', 'p1').subscribe();
    http
      .expectOne('/api/career/materials/m1/proposals/p1/reject')
      .flush({ success: true, data: {} });
  });
  it('maps resume conflicts and preconditions', () => {
    const cases: [number, string | undefined, string][] = [
      [409, 'CareerResumeUnsupportedClaim', 'unsupportedClaim'],
      [409, 'CareerOccupationRequired', 'occupationRequired'],
      [412, undefined, 'conflict'],
      [428, undefined, 'precondition'],
    ];
    for (const [status, code, kind] of cases) {
      let got = '';
      service.getResumeMaterial('m1').subscribe({ error: e => (got = e.kind) });
      http
        .expectOne('/api/career/materials/m1')
        .flush({ success: false, error: { code, message: 'x' } }, { status, statusText: 'x' });
      expect(got).toBe(kind);
    }
  });
  it('lists materials of both kinds or one kind', () => {
    service.listMaterials().subscribe();
    http
      .expectOne('/api/career/materials')
      .flush({ success: true, data: { materials: [{ id: 'a', kind: 'summary' }] } });
    service.listMaterials('summary').subscribe();
    http
      .expectOne('/api/career/materials?kind=summary')
      .flush({ success: true, data: { materials: [] } });
  });
  it('starts a professional summary run', () => {
    service.createRun('k1', 'professional_summary').subscribe();
    expect(http.expectOne('/api/career/runs').request.body).toEqual({
      task: 'professional_summary',
    });
  });
  it('creates, lists and downloads exports', () => {
    service.createExport('m1', { format: 'pdf', version: 2, includePhoto: false }).subscribe();
    const create = http.expectOne('/api/career/materials/m1/exports');
    expect(create.request.method).toBe('POST');
    expect(create.request.body).toEqual({ format: 'pdf', version: 2, includePhoto: false });
    create.flush({ success: true, data: { id: 'e1' } });
    service.listExports('m1').subscribe();
    http
      .expectOne('/api/career/materials/m1/exports')
      .flush({ success: true, data: { exports: [] } });
    let size = 0;
    service.downloadExport('e1').subscribe(b => (size = b.size));
    const file = http.expectOne('/api/career/exports/e1');
    expect(file.request.responseType).toBe('blob');
    file.flush(new Blob(['abc']));
    expect(size).toBe(3);
  });
  it('maps export failures', () => {
    const cases: [number, string | undefined, string][] = [
      [400, 'CareerExportPhotoUnavailable', 'exportPhotoUnavailable'],
      [410, 'CareerExportExpired', 'exportExpired'],
      [410, undefined, 'exportExpired'],
    ];
    for (const [status, code, kind] of cases) {
      let got = '';
      service.createExport('m1', { format: 'docx' }).subscribe({ error: e => (got = e.kind) });
      http
        .expectOne('/api/career/materials/m1/exports')
        .flush({ success: false, error: { code, message: 'x' } }, { status, statusText: 'x' });
      expect(got).toBe(kind);
    }
    let expired = '';
    service.downloadExport('e1').subscribe({ error: e => (expired = e.kind) });
    http.expectOne('/api/career/exports/e1').flush(new Blob(), { status: 410, statusText: 'Gone' });
    expect(expired).toBe('exportExpired');
  });
  it('calls the privacy endpoints and maps reauth', () => {
    service.getRetention().subscribe();
    const r = http.expectOne('/api/career/privacy/retention');
    expect(r.request.method).toBe('GET');
    r.flush({ success: true, data: { items: [], processors: [] } });
    let size = 0;
    service.downloadPrivacyExport().subscribe(b => (size = b.size));
    const file = http.expectOne('/api/career/privacy/export');
    expect(file.request.responseType).toBe('blob');
    file.flush(new Blob(['{}']));
    expect(size).toBe(2);
    service.requestDeletion('career_profile').subscribe();
    const d = http.expectOne('/api/career/privacy/deletions');
    expect(d.request.method).toBe('POST');
    expect(d.request.body).toEqual({ scope: 'career_profile' });
    d.flush({ success: true, data: { id: 'd1' } });
    service.getDeletion('d1').subscribe();
    http.expectOne('/api/career/privacy/deletions/d1').flush({ success: true, data: { id: 'd1' } });
    service.retryDeletion('d1').subscribe();
    const retry = http.expectOne('/api/career/privacy/deletions/d1/retry');
    expect(retry.request.method).toBe('POST');
    retry.flush({ success: true, data: { id: 'd1' } });
    let kind = '';
    service.requestDeletion('raw_documents').subscribe({ error: e => (kind = e.kind) });
    http
      .expectOne('/api/career/privacy/deletions')
      .flush(
        { success: false, error: { code: 'CareerReauthRequired', message: 'x' } },
        { status: 401, statusText: 'Unauthorized' }
      );
    expect(kind).toBe('reauth');
  });
});

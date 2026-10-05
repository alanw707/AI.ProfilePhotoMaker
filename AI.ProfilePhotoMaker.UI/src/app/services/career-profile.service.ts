import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { catchError, map, Observable, throwError } from 'rxjs';
import { ConfigService } from './config.service';

export interface ProfileFacts {
  currentTitle: string;
  industry?: string | null;
  yearsExperience?: number | null;
  location?: string | null;
  summary?: string | null;
  skills: string[];
  highlights: string[];
  workArrangement?: string | null;
}
export interface GoalFacts {
  targetRole: string;
  targetLocation?: string | null;
  workArrangement?: string | null;
  desiredPayMin?: number | null;
  desiredPayMax?: number | null;
  weeklyEffortHours?: number | null;
}
export interface Provenance {
  source: string;
  confirmedAt: string;
  restoredFromVersion?: number | null;
  sourceProposalId?: string | null;
}
export interface CareerProfileDto {
  id: string;
  version: number;
  etag: string;
  facts: ProfileFacts;
  provenance: Provenance;
  createdAt: string;
  updatedAt: string;
}
export interface CareerGoalDto {
  id: string;
  version: number;
  etag: string;
  goal: GoalFacts;
  basedOnProfileVersion: number | null;
  isStale: boolean;
  occupation?: GoalOccupation | null;
  provenance: Provenance;
  createdAt: string;
  updatedAt: string;
}
export interface GoalOccupation {
  code: string;
  title: string;
  referenceRelease: string;
  matchId: string;
}
export interface ProfileVersion {
  version: number;
  createdAt: string;
  source: string;
  currentTitle: string;
  isActive: boolean;
}
export interface ProfileVersionDetail {
  version: number;
  facts: ProfileFacts;
  provenance: Provenance;
  createdAt: string;
  isActive: boolean;
}
export interface GoalVersion {
  version: number;
  createdAt: string;
  targetRole: string;
  isActive: boolean;
}
export interface GoalVersionDetail {
  version: number;
  goal: GoalFacts;
  basedOnProfileVersion: number | null;
  provenance: Provenance;
  createdAt: string;
  isActive: boolean;
}
export type ResumeState = 'ready' | 'unreadable' | 'failed';
export interface ResumeDocumentDto {
  id: string;
  fileName: string;
  format: string;
  sizeBytes: number;
  pageCount: number | null;
  state: ResumeState;
  failureCode: string | null;
  proposalId: string | null;
  uploadedAt: string;
  expiresAt: string;
}
export type ProposalField =
  | 'currentTitle'
  | 'industry'
  | 'yearsExperience'
  | 'location'
  | 'summary'
  | 'skills'
  | 'highlights';
export interface ProposalItem {
  id: string;
  field: ProposalField;
  value: string;
  currentValue: string | null;
  page: number | null;
  section: string | null;
  excerpt: string | null;
  flags: ('conflict' | 'ambiguous')[];
}
export interface CareerProfileProposalDto {
  id: string;
  source: 'resume' | 'pasted' | 'agent';
  resumeId: string | null;
  baseProfileVersion: number | null;
  status: 'pending' | 'accepted' | 'dismissed';
  isStale: boolean;
  items: ProposalItem[];
  createdAt: string;
}
export interface CareerPhotoDto {
  id: number;
  imageUrl: string;
  createdAt: string;
  style: string | null;
  isWatermarkedPreview: boolean;
}
export interface CareerPhotoEntitlement {
  packageCode: string;
  packageName: string;
  remainingCandidates: number;
  remainingRefinements: number;
  remainingPremiumAugmentations: number;
  platformExportKitAvailable: boolean;
  expiresAt: string | null;
}
export interface CareerPhotoList {
  photos: CareerPhotoDto[];
  selectedPhotoId: number | null;
  selectedPhotoAvailable: boolean;
  entitlements: CareerPhotoEntitlement[];
}
export interface CareerPhotoSelection {
  selectedPhotoId: number;
  careerGoalId: string | null;
  selectedAt: string;
}
export type CareerRunStatus =
  | 'queued'
  | 'working'
  | 'needs_input'
  | 'completed'
  | 'failed'
  | 'cancelled';
export interface CareerRunStep {
  ordinal: number;
  kind: string;
  name: string;
  label: string;
  status: string;
  completedAt: string | null;
}
export interface CareerRunChoice {
  value: string;
  label: string;
}
export interface CareerRunQuestion {
  id: string;
  text: string;
  maxLength: number;
  /** Present when the answer must be one of a fixed list. */
  choices?: CareerRunChoice[];
}
export type CareerRunTask =
  | 'profile_summary'
  | 'occupation_match'
  | 'market_brief'
  | 'pay_analysis';
export interface CareerRunAllowance {
  used: number;
  reserved: number;
  limit: number;
  periodStart: string;
}
export interface CareerRunDto {
  id: string;
  task: CareerRunTask;
  status: CareerRunStatus;
  createdAt: string;
  updatedAt: string;
  completedAt: string | null;
  pinnedProfileVersion: number | null;
  pinnedGoalVersion: number | null;
  steps: CareerRunStep[];
  question: CareerRunQuestion | null;
  proposalId: string | null;
  occupationMatchId?: string | null;
  marketBriefId?: string | null;
  payAnalysisId?: string | null;
  profileChanged: boolean;
  errorCode: string | null;
  allowance: CareerRunAllowance;
}
export interface CareerRunList {
  runs: CareerRunDto[];
  allowance: CareerRunAllowance;
}
export type OccupationMatchStatus = 'proposed' | 'confirmed' | 'dismissed' | 'unsupported';
export type OccupationStrength = 'strong' | 'moderate' | 'weak';
export interface OccupationReference {
  name: string;
  release: string;
  releaseDate: string;
  taxonomy: string;
  license: string;
  licenseUrl: string;
  url: string;
  attribution: string;
}
export interface OccupationReferenceInfo extends OccupationReference {
  occupationCount: number;
}
export interface OccupationEvidence {
  kind: 'duty' | 'skill';
  profileField: string;
  profileIndex: number;
  profileText: string;
  referenceKind: string;
  referenceId: string | null;
  referenceText: string;
}
export interface OccupationCandidate {
  code: string;
  title: string;
  description: string;
  strength: OccupationStrength;
  evidence: OccupationEvidence[];
  titleMatched: boolean;
  missingEvidence: string[];
  knownGaps: string[];
  unsupportedSkills: string[];
}
export interface OccupationMatchDto {
  id: string;
  runId: string;
  status: OccupationMatchStatus;
  pinnedProfileVersion: number;
  pinnedGoalVersion: number | null;
  profileChanged: boolean;
  reference: OccupationReference;
  matcherVersion: string;
  candidates: OccupationCandidate[];
  clarification: { question: string; answer: string } | null;
  guidance: string | null;
  confirmedCode: string | null;
  confirmedIntoGoalVersion: number | null;
  createdAt: string;
  decidedAt: string | null;
}
export type MarketFigureStatus = 'available' | 'not_available' | 'top_coded' | 'not_published';
export type MarketFigureUnit =
  | 'usd_per_year'
  | 'usd_per_hour'
  | 'jobs'
  | 'jobs_thousands'
  | 'per_1000_jobs'
  | 'ratio'
  | 'percent'
  | 'percent_rse'
  | 'text';
export interface MarketFigure {
  key: string;
  label: string;
  value: number | string | null;
  status: MarketFigureStatus;
  unit: MarketFigureUnit;
  areaCode: string;
  areaTitle: string;
  sourceId: string;
}
export interface MarketAlternative {
  code: string;
  title: string;
  /** Set when BLS publishes this occupation only under a broader or shared group. */
  note: string | null;
  figures: MarketFigure[];
}
export type MarketSectionKey = 'wages' | 'employment' | 'outlook' | 'alternatives';
export interface MarketSection {
  key: MarketSectionKey;
  title: string;
  status: 'complete' | 'unavailable' | 'failed';
  reason: string | null;
  note: string;
  figures: MarketFigure[];
  items: MarketAlternative[];
}
export interface MarketSource {
  id: string;
  name: string;
  publisher: string;
  referencePeriod: string;
  publishedOn: string;
  url: string;
  definitionsUrl: string;
  license: string;
  citation: string;
  definition: string;
  coverage: string;
}
export interface MarketBriefSummary {
  id: string;
  occupationCode: string;
  occupationTitle: string;
  areaTitle: string | null;
  status: 'complete' | 'partial';
  stale: boolean;
  createdAt: string;
}
export interface MarketBriefDto {
  id: string;
  runId: string;
  status: 'complete' | 'partial';
  occupation: {
    code: string;
    title: string;
    published: Record<'oews' | 'projections', { code: string; match: string } | null>;
  };
  location: {
    input: string | null;
    resolution: 'metro' | 'state' | 'national_only' | 'unresolved';
    local: { code: string; title: string; type: string } | null;
  };
  pinned: {
    profileVersion: number;
    goalVersion: number;
    oewsRelease: string;
    projectionsRelease: string;
  };
  stale: boolean;
  staleReasons: ('goal_changed' | 'profile_changed' | 'occupation_changed')[];
  dataStale: boolean;
  sections: MarketSection[];
  nextAction: { label: string; route: string } | null;
  sources: MarketSource[];
  createdAt: string;
}
export interface MarketReferenceInfo {
  sources: MarketSource[];
  areaCount: number;
  occupationCount: number;
}
export interface PayBenchmarkSection {
  key: 'benchmark';
  title: string;
  status: 'complete' | 'unavailable' | 'failed';
  reason: string | null;
  label: string;
  note: string;
  figures: MarketFigure[];
}
export interface PayCohort {
  included: number;
  excluded: number;
  employers: number;
  largestEmployerShare: number;
  concentrated: boolean;
  sensitive: boolean;
  exclusionReasons: Record<string, number>;
}
export interface PayPersonalizedSection {
  key: 'personalized';
  title: string;
  status: 'complete' | 'unavailable' | 'insufficient_evidence' | 'failed';
  reason: string | null;
  interval: { low: number; high: number; unit: string; definition: string } | null;
  cohort: PayCohort;
  note: string;
}
export interface PayScenarioSection {
  key: 'scenario';
  title: string;
  status: 'complete' | 'unavailable';
  requestedAnnual: number | null;
  benchmarkMedianAnnual: number | null;
  gapAnnual: number | null;
  gapPercent: number | null;
  benchmarkAreaCode?: string | null;
  benchmarkAreaTitle?: string | null;
  requestedPaySource?: string | null;
  note: string;
}
export type PaySection = PayBenchmarkSection | PayPersonalizedSection | PayScenarioSection;
export interface PayGate {
  gateId: string;
  requirement: string;
  status: string;
  evidence: string;
}
export interface PayQualification {
  personalizedAllowed: boolean;
  blockedReasons?: string[];
  gates: PayGate[];
}
export interface PayAnalysisSummary {
  id: string;
  occupationCode: string;
  occupationTitle: string;
  areaTitle: string | null;
  status: 'complete' | 'partial';
  personalizedAvailable: boolean;
  stale: boolean;
  createdAt: string;
}
export interface PayAnalysisDto {
  id: string;
  runId: string;
  status: 'complete' | 'partial';
  occupation: {
    code: string;
    title: string;
    publishedCode: string;
    mapping: 'exact' | 'broad' | 'shared';
  };
  location: {
    input: string | null;
    resolution: 'metro' | 'state' | 'national_only' | 'unresolved';
    local: { code: string; title: string; type: string } | null;
  };
  pinned: {
    profileVersion: number;
    goalVersion: number;
    oewsRelease: string;
    oewsSnapshotSha256: string;
    projectionsRelease: string;
    ruleVersion: string;
    observationSourceId: string | null;
  };
  inputHash: string;
  stale: boolean;
  staleReasons: string[];
  sections: PaySection[];
  blockedReasons: string[];
  qualification: PayQualification;
  sources: MarketSource[];
  createdAt: string;
}
export interface PayRecomputeResult {
  matches: boolean;
  inputHash: string;
  storedInputHash: string;
  differences: string[];
  sections: PaySection[];
}
export interface CareerApiError {
  /** Server error code (for example CareerRunNotWaiting), when one was sent. */
  code?: string;
  kind:
    | 'tooLarge'
    | 'unsupported'
    | 'rejected'
    | 'scannerUnavailable'
    | 'conflict'
    | 'disabled'
    | 'notFound'
    | 'validation'
    | 'precondition'
    | 'unauthorized'
    | 'alreadyExists'
    | 'preview'
    | 'allowance'
    | 'unavailable'
    | 'profileRequired'
    | 'idempotencyMismatch'
    | 'notWaiting'
    | 'matchStale'
    | 'notConfirmable'
    | 'goalRequired'
    | 'occupationRequired'
    | 'unknown';
  message: string;
  fieldErrors?: Record<string, string>;
  currentVersion?: number;
  /** Extra explanation for 415 (for example "encrypted PDF"). */
  detail?: string;
  retryAfterSeconds?: number;
}
interface Envelope<T> {
  success: boolean;
  data: T;
  error?: {
    code?: string;
    message?: string;
    fieldErrors?: Record<string, string>;
    currentVersion?: number;
    detail?: string;
    retryAfterSeconds?: number;
  };
}

/**
 * Version of the resume consent notice shown on the import page. The server rejects
 * uploads that agreed to a different version, so change both together.
 */
export const RESUME_CONSENT_VERSION = 'resume-notice-2026-10-04';

@Injectable({ providedIn: 'root' })
export class CareerProfileService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(ConfigService);
  private profileEtag: string | null = null;
  private goalEtag: string | null = null;
  private url(path: string) {
    return this.config.buildApiEndpoint(`career/${path}`);
  }

  private request<T>(
    method: string,
    path: string,
    body?: unknown,
    resource?: 'profile' | 'goal',
    match = false,
    extraHeaders: Record<string, string> = {}
  ): Observable<T> {
    const etag = resource === 'profile' ? this.profileEtag : this.goalEtag;
    const headers = new HttpHeaders(
      match && etag ? { ...extraHeaders, 'If-Match': etag } : extraHeaders
    );
    return this.http
      .request<Envelope<T>>(method, this.url(path), { body, headers, observe: 'response' })
      .pipe(
        map(response => {
          const data = response.body?.data;
          if (!response.body?.success || data === undefined) {
            throw { kind: 'unknown', message: 'Unable to read the saved data.' } as CareerApiError;
          }
          const value = data as { etag?: string };
          if (resource === 'profile') {
            this.profileEtag = response.headers.get('ETag') ?? value.etag ?? this.profileEtag;
          }
          if (resource === 'goal') {
            this.goalEtag = response.headers.get('ETag') ?? value.etag ?? this.goalEtag;
          }
          return data;
        }),
        catchError(error => throwError(() => this.mapError(error)))
      );
  }
  private mapError(error: unknown): CareerApiError {
    if (!(error instanceof HttpErrorResponse)) {
      return error as CareerApiError;
    }
    const payload = error.error?.error;
    const kinds: Record<number, CareerApiError['kind']> = {
      400: 'validation',
      401: 'unauthorized',
      403: 'disabled',
      404: 'notFound',
      409: 'alreadyExists',
      412: 'conflict',
      413: 'tooLarge',
      415: 'unsupported',
      422: 'rejected',
      428: 'precondition',
      503: 'scannerUnavailable',
    };
    const retryHeader = Number(error.headers?.get('Retry-After'));
    const codeKinds: Record<string, CareerApiError['kind']> = {
      CareerPhotoIsPreview: 'preview',
      CareerAllowanceExhausted: 'allowance',
      CareerModelUnavailable: 'unavailable',
      CareerIdempotencyMismatch: 'idempotencyMismatch',
      CareerProfileRequired: 'profileRequired',
      CareerRunNotWaiting: 'notWaiting',
      CareerMatchStale: 'matchStale',
      CareerMatchNotConfirmable: 'notConfirmable',
      CareerGoalRequired: 'goalRequired',
      CareerOccupationRequired: 'occupationRequired',
      CareerReferenceUnavailable: 'unavailable',
    };
    const kind = codeKinds[payload?.code ?? ''] ?? kinds[error.status] ?? 'unknown';
    return {
      kind,
      code: payload?.code,
      message: payload?.message ?? 'Unable to complete the request.',
      fieldErrors: payload?.fieldErrors,
      currentVersion: payload?.currentVersion,
      detail: payload?.detail,
      retryAfterSeconds: payload?.retryAfterSeconds ?? (retryHeader > 0 ? retryHeader : undefined),
    };
  }
  getProfile() {
    return this.request<CareerProfileDto>('GET', 'profile', undefined, 'profile');
  }
  saveProfile(facts: ProfileFacts & { confirmed: boolean }) {
    return this.request<CareerProfileDto>('PUT', 'profile', facts, 'profile', true);
  }
  profileVersions() {
    return this.request<ProfileVersion[]>('GET', 'profile/versions');
  }
  profileVersion(version: number) {
    return this.request<ProfileVersionDetail>('GET', `profile/versions/${version}`);
  }
  restoreProfile(version: number) {
    return this.request<CareerProfileDto>(
      'POST',
      `profile/versions/${version}/restore`,
      undefined,
      'profile',
      true
    );
  }
  getGoal() {
    return this.request<CareerGoalDto>('GET', 'goals', undefined, 'goal');
  }
  createGoal(goal: GoalFacts & { confirmed: boolean }) {
    return this.request<CareerGoalDto>('POST', 'goals', goal, 'goal');
  }
  updateGoal(id: string, goal: GoalFacts & { confirmed: boolean }) {
    return this.request<CareerGoalDto>(
      'PATCH',
      `goals/${encodeURIComponent(id)}`,
      goal,
      'goal',
      true
    );
  }
  goalVersions(id: string) {
    return this.request<GoalVersion[]>('GET', `goals/${encodeURIComponent(id)}/versions`);
  }
  goalVersion(id: string, version: number) {
    return this.request<GoalVersionDetail>(
      'GET',
      `goals/${encodeURIComponent(id)}/versions/${version}`
    );
  }
  restoreGoal(id: string, version: number) {
    return this.request<CareerGoalDto>(
      'POST',
      `goals/${encodeURIComponent(id)}/versions/${version}/restore`,
      undefined,
      'goal',
      true
    );
  }

  uploadResume(file: File, consent: boolean) {
    const form = new FormData();
    form.append('file', file, file.name);
    form.append('consent', String(consent));
    form.append('consentVersion', RESUME_CONSENT_VERSION);
    return this.request<ResumeDocumentDto>('POST', 'resumes', form);
  }
  listResumes() {
    return this.request<ResumeDocumentDto[]>('GET', 'resumes');
  }
  getResume(id: string) {
    return this.request<ResumeDocumentDto>('GET', `resumes/${encodeURIComponent(id)}`);
  }
  deleteResume(id: string) {
    return this.http
      .delete(this.url(`resumes/${encodeURIComponent(id)}`))
      .pipe(catchError(error => throwError(() => this.mapError(error))));
  }
  downloadResume(id: string) {
    return this.http
      .get(this.url(`resumes/${encodeURIComponent(id)}/file`), { responseType: 'blob' })
      .pipe(catchError(error => throwError(() => this.mapError(error))));
  }
  createPastedProposal(text: string) {
    return this.request<CareerProfileProposalDto>('POST', 'profile/proposals', { text });
  }
  getProposal(id: string) {
    return this.request<CareerProfileProposalDto>(
      'GET',
      `profile/proposals/${encodeURIComponent(id)}`
    );
  }
  acceptProposal(id: string, itemIds: string[]) {
    return this.request<CareerProfileDto>(
      'POST',
      `profile/proposals/${encodeURIComponent(id)}/accept`,
      { itemIds },
      'profile',
      true
    );
  }
  dismissProposal(id: string) {
    return this.request<CareerProfileProposalDto>(
      'POST',
      `profile/proposals/${encodeURIComponent(id)}/dismiss`
    );
  }

  listPhotos() {
    return this.request<CareerPhotoList>('GET', 'photos');
  }
  selectPhoto(processedImageId: number) {
    return this.request<CareerPhotoSelection>('PUT', 'photos/selection', { processedImageId });
  }
  clearPhoto() {
    return this.http
      .delete(this.url('photos/selection'))
      .pipe(catchError(error => throwError(() => this.mapError(error))));
  }

  createRun(idempotencyKey: string, task: CareerRunTask = 'profile_summary') {
    return this.request<CareerRunDto>('POST', 'runs', { task }, undefined, false, {
      'Idempotency-Key': idempotencyKey,
    });
  }
  getRun(id: string) {
    return this.request<CareerRunDto>('GET', `runs/${encodeURIComponent(id)}`);
  }
  listRuns() {
    return this.request<CareerRunList>('GET', 'runs');
  }
  answerRun(id: string, questionId: string, answer: string) {
    return this.request<CareerRunDto>('POST', `runs/${encodeURIComponent(id)}/answers`, {
      questionId,
      answer,
    });
  }
  cancelRun(id: string) {
    return this.request<CareerRunDto>('POST', `runs/${encodeURIComponent(id)}/cancel`);
  }

  getOccupationMatch(id: string) {
    return this.request<OccupationMatchDto>('GET', `occupation-matches/${encodeURIComponent(id)}`);
  }
  /** Saves the chosen occupation into the goal; needs the goal ETag from getGoal(). */
  confirmOccupationMatch(id: string, occupationCode: string, goalEtag: string) {
    return this.request<CareerGoalDto>(
      'POST',
      `occupation-matches/${encodeURIComponent(id)}/confirm`,
      { occupationCode },
      'goal',
      false,
      { 'If-Match': goalEtag }
    );
  }
  dismissOccupationMatch(id: string) {
    return this.request<OccupationMatchDto>(
      'POST',
      `occupation-matches/${encodeURIComponent(id)}/dismiss`
    );
  }
  getOccupationReference() {
    return this.request<OccupationReferenceInfo>('GET', 'occupations/reference');
  }

  listPayAnalyses() {
    return this.request<{ analyses: PayAnalysisSummary[] }>('GET', 'pay-analyses');
  }
  getPayAnalysis(id: string) {
    return this.request<PayAnalysisDto>('GET', `pay-analyses/${encodeURIComponent(id)}`);
  }
  recomputePayAnalysis(id: string) {
    return this.request<PayRecomputeResult>(
      'POST',
      `pay-analyses/${encodeURIComponent(id)}/recompute`
    );
  }
  getPayQualification() {
    return this.request<PayQualification>('GET', 'pay/qualification');
  }

  listMarketBriefs() {
    return this.request<{ briefs: MarketBriefSummary[] }>('GET', 'market-briefs');
  }
  getMarketBrief(id: string) {
    return this.request<MarketBriefDto>('GET', `market-briefs/${encodeURIComponent(id)}`);
  }
  getMarketReference() {
    return this.request<MarketReferenceInfo>('GET', 'market/reference');
  }
}

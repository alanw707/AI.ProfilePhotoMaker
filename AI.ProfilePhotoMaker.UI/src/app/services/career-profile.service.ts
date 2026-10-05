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
  preferredArea?: PreferredArea | null;
  provenance: Provenance;
  createdAt: string;
  updatedAt: string;
}
export type MarketLevel = 'national' | 'state' | 'metro';
export interface PreferredArea {
  code: string;
  title: string;
  level: MarketLevel;
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
  | 'pay_analysis'
  | 'roadmap'
  | 'targeted_resume';
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
  roadmapId?: string | null;
  materialId?: string | null;
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
export interface MarketMetric {
  key: string;
  label: string;
  unit: MarketFigureUnit;
  measure: string;
  supported: boolean;
  /** Machine reason when unsupported (for example national_only_source). */
  reason: string | null;
  geographyLevels: MarketLevel[];
}
export interface MarketComparisonArea {
  areaCode: string;
  areaTitle: string;
  type: MarketLevel;
  value: number | null;
  status: MarketFigureStatus;
  rank: number | null;
  rankedOf: number;
  selected: boolean;
  shareOfNationalEmployment?: number | null;
}
export interface MarketComparison {
  occupation: { code: string; title: string; publishedCode: string; mapping: string };
  metric: Omit<MarketMetric, 'geographyLevels'>;
  level: Exclude<MarketLevel, 'national'>;
  national: {
    areaCode: string;
    areaTitle: string;
    value: number | null;
    status: MarketFigureStatus;
  };
  reference: {
    release: string;
    publishedOn: string;
    coverage: string;
    definitionsUrl: string;
    citation: string;
  };
  areas: MarketComparisonArea[];
  selectionLimit: number;
  truncated: boolean;
}
export interface MarketComparisonQuery {
  metric: string;
  level: string;
  areas?: string[];
  q?: string;
}
export type RemoteEligibility = 'eligible' | 'ineligible' | 'unknown';
export type RemoteFilter = 'all' | RemoteEligibility;
export interface JobObservationsQuery {
  area?: string;
  eligibleOnly?: boolean;
  remote?: RemoteFilter;
  q?: string;
}
export interface JobCoverageCounts {
  fetched: number;
  matched: number;
  shown: number;
  duplicateIds: number;
  duplicateReposts: number;
  expired: number;
  remoteUnknownExcluded: number;
  remoteIneligibleExcluded: number;
  otherLocationExcluded: number;
  keywordExcluded: number;
  remoteFilterExcluded: number;
  cappedByLimit: number;
}
export interface JobCoverage {
  available: boolean;
  reason: 'source_not_configured' | 'source_unavailable' | 'occupation_required' | null;
  sourceId: string;
  sourceName: string;
  coverage: string;
  attribution: string;
  sourceUrl: string;
  retrievedAt: string | null;
  postedFrom: string | null;
  postedTo: string | null;
  counts: JobCoverageCounts;
}
export interface JobLocation {
  city: string;
  state: string;
  areaCode: string | null;
  match: 'user_area' | 'other';
}
export interface JobPay {
  min: number | null;
  max: number | null;
  unit: 'usd_per_year' | 'usd_per_hour';
  basis: 'annual' | 'hourly';
  status: 'available' | 'not_available' | 'top_coded';
}
export interface JobObservation {
  observationId: string;
  title: string;
  organization: string;
  locations: JobLocation[];
  multiLocation: boolean;
  pay: JobPay;
  postedOn: string | null;
  closesOn: string | null;
  remoteEligibility: RemoteEligibility;
  remoteNote: string | null;
  series?: string | null;
  grade?: string | null;
  sourceUrl?: string | null;
  sourceId: string;
}
export interface JobObservations {
  occupation: { code: string; title: string };
  area: { input: string | null; resolution: string; code: string | null; title: string | null };
  coverage: JobCoverage;
  preferences: { areaCode: string | null; stalePreference: boolean; note: string | null };
  observations: JobObservation[];
  truncated: boolean;
  note: string;
}
export interface JobSourceInfo {
  sourceId: string;
  name: string;
  configured: boolean;
  coverage: string;
  attribution: string;
  sourceUrl: string;
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
export type RoadmapStatus = 'proposed' | 'accepted' | 'dismissed';
export type RoadmapOptionKey = 'closest_fit' | 'higher_ambition' | 'steadier_transition';
export interface RoadmapTask {
  id: string;
  title: string;
  effortHours: number;
  dependsOn: string[];
}
export interface RoadmapRationale {
  text: string;
  sourceId: string;
  release: string;
}
export interface RoadmapOption {
  key: RoadmapOptionKey;
  occupationCode: string;
  title: string;
  rationale: RoadmapRationale[];
  assumptions: string[];
  missingEvidence: string[];
  timelineNote: string;
  thisWeek: RoadmapTask[];
  milestones: { day: number; tasks: RoadmapTask[] }[];
}
export interface RoadmapDto {
  id: string;
  version: number;
  status: RoadmapStatus;
  selectedOption: RoadmapOptionKey | null;
  pinned: {
    profileVersion: number;
    goalVersion: number;
    occupationCode: string;
    marketBriefId: string | null;
    payAnalysisId: string | null;
  };
  stale: boolean;
  staleReasons: string[];
  weeklyEffortHours: number | null;
  options: RoadmapOption[];
  omittedOptions: { key: RoadmapOptionKey; reason: string }[];
  lowTimeNote: string | null;
  goalUnchanged?: boolean;
  createdAt?: string;
}
export type TaskStatus = 'not_started' | 'in_progress' | 'done' | 'blocked';
export interface TaskProgress {
  taskId: string;
  title: string;
  origin: 'generated' | 'human';
  milestoneDay: 0 | 30 | 60 | 90;
  dependsOn: string[];
  status: TaskStatus;
  effectiveStatus: TaskStatus;
  blockedBy: string[];
  effortHours: number | null;
  outputNote: string | null;
  linkedMaterialId: string | null;
  linkedMaterialMissing: boolean;
  help: string;
  etag: string;
}
export interface RoadmapProgress {
  roadmapId: string;
  version: number;
  tasks: TaskProgress[];
}
export interface TaskProgressUpdate {
  status?: TaskStatus;
  effortHours?: number;
  outputNote?: string;
  linkedMaterialId?: string | null;
}
export interface NewHumanTask {
  title: string;
  effortHours: number;
  milestoneDay: number;
  dependsOn: string[];
}
export interface ReplanChange {
  id: string;
  kind: 'added' | 'removed' | 'changed';
  taskId: string;
  title: string;
  fields: { field: string; before: unknown; after: unknown }[];
  rationale: string;
}
export interface ReplanDto {
  id: string;
  baseVersion: number;
  changes: ReplanChange[];
  preserved: { taskId: string; title: string; reason: 'done' | 'has_output' | 'human' }[];
}
export interface RoadmapSummary {
  id: string;
  version: number;
  status: RoadmapStatus;
  stale: boolean;
  optionCount: number;
  createdAt: string;
}
export interface PayRecomputeResult {
  matches: boolean;
  inputHash: string;
  storedInputHash: string;
  differences: string[];
  sections: PaySection[];
}
export type ResumeSectionKey = 'headline' | 'summary' | 'experience_highlights' | 'skills';
export interface ResumeLine {
  id: string;
  text: string;
  factIds: string[];
  origin: 'generated' | 'human';
}
export interface ResumeSection {
  key: ResumeSectionKey;
  lines: ResumeLine[];
}
export interface ResumeContact {
  name: boolean;
  email: boolean;
  phone: boolean;
  location: boolean;
  links: boolean;
}
export interface ResumeMaterialSummary {
  id: string;
  title: string;
  stale: boolean;
  currentVersion: number;
  updatedAt: string;
}
export interface ResumeMaterialDto {
  id: string;
  title: string;
  etag: string;
  currentVersion: number;
  pinned: { profileVersion: number; goalVersion: number; occupationCode: string };
  stale: boolean;
  staleReasons: string[];
  contact: ResumeContact;
  sections: ResumeSection[];
  questions: { id: string; factId: string; text: string }[];
  facts: { id: string; text: string }[];
}
export interface ResumeVersionInfo {
  number: number;
  author: string;
  createdAt: string;
}
export interface ResumeChange {
  id: string;
  kind: 'added' | 'removed' | 'changed';
  section: ResumeSectionKey;
  before: string | null;
  after: string | null;
  factIds: string[];
}
export interface ResumeProposalDto {
  id: string;
  baseVersion: number;
  changes: ResumeChange[];
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
    | 'unsupportedClaim'
    | 'roadmapCycle'
    | 'roadmapNotProposed'
    | 'roadmapNotAccepted'
    | 'replanStale'
    | 'replanClosed'
    | 'metricUnsupported'
    | 'areaNotFound'
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
      CareerResumeUnsupportedClaim: 'unsupportedClaim',
      CareerRoadmapCycle: 'roadmapCycle',
      CareerRoadmapNotProposed: 'roadmapNotProposed',
      CareerRoadmapNotAccepted: 'roadmapNotAccepted',
      CareerReplanStale: 'replanStale',
      CareerReplanClosed: 'replanClosed',
      CareerMetricUnsupported: 'metricUnsupported',
      CareerAreaNotFound: 'areaNotFound',
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

  createRun(idempotencyKey: string, task: CareerRunTask = 'profile_summary', materialId?: string) {
    const body = materialId ? { task, materialId } : { task };
    return this.request<CareerRunDto>('POST', 'runs', body, undefined, false, {
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

  listRoadmaps() {
    return this.request<{ roadmaps: RoadmapSummary[] }>('GET', 'roadmaps');
  }
  getRoadmap(id: string) {
    return this.request<RoadmapDto>('GET', `roadmaps/${encodeURIComponent(id)}`);
  }
  /** Records the chosen path; the goal itself is not changed. Needs the goal ETag. */
  acceptRoadmap(id: string, optionKey: RoadmapOptionKey, goalEtag: string) {
    return this.request<RoadmapDto>(
      'POST',
      `roadmaps/${encodeURIComponent(id)}/accept`,
      { optionKey },
      undefined,
      false,
      { 'If-Match': goalEtag }
    );
  }
  dismissRoadmap(id: string) {
    return this.request<RoadmapDto>('POST', `roadmaps/${encodeURIComponent(id)}/dismiss`);
  }
  updateRoadmapTask(id: string, taskId: string, effortHours: number) {
    return this.request<RoadmapDto>(
      'PUT',
      `roadmaps/${encodeURIComponent(id)}/tasks/${encodeURIComponent(taskId)}`,
      { effortHours }
    );
  }
  getProgress(id: string) {
    return this.request<RoadmapProgress>('GET', `roadmaps/${encodeURIComponent(id)}/progress`);
  }
  /** Saves one task's progress; the task etag goes in If-Match so edits elsewhere are caught. */
  updateTaskProgress(id: string, taskId: string, update: TaskProgressUpdate, etag: string) {
    return this.request<TaskProgress>(
      'PUT',
      `roadmaps/${encodeURIComponent(id)}/progress/${encodeURIComponent(taskId)}`,
      update,
      undefined,
      false,
      { 'If-Match': etag }
    );
  }
  addHumanTask(id: string, task: NewHumanTask) {
    return this.request<TaskProgress>('POST', `roadmaps/${encodeURIComponent(id)}/tasks`, task);
  }
  replan(id: string) {
    return this.request<ReplanDto>('POST', `roadmaps/${encodeURIComponent(id)}/replan`);
  }
  getReplan(replanId: string) {
    return this.request<ReplanDto>('GET', `replans/${encodeURIComponent(replanId)}`);
  }
  applyReplan(replanId: string, acceptedChangeIds: string[]) {
    return this.request<RoadmapDto>('POST', `replans/${encodeURIComponent(replanId)}/apply`, {
      acceptedChangeIds,
    });
  }
  rejectReplan(replanId: string) {
    return this.request<unknown>('POST', `replans/${encodeURIComponent(replanId)}/reject`);
  }

  listResumeMaterials() {
    return this.request<{ materials: ResumeMaterialSummary[] }>('GET', 'materials?kind=resume');
  }
  getResumeMaterial(id: string) {
    return this.request<ResumeMaterialDto>('GET', `materials/${encodeURIComponent(id)}`);
  }
  saveResume(
    id: string,
    body: { sections: ResumeSection[]; contact: ResumeContact },
    etag: string
  ) {
    return this.request<ResumeMaterialDto>(
      'PUT',
      `materials/${encodeURIComponent(id)}`,
      body,
      undefined,
      false,
      { 'If-Match': etag }
    );
  }
  listResumeVersions(id: string, page = 1) {
    return this.request<{ versions: ResumeVersionInfo[]; total: number }>(
      'GET',
      `materials/${encodeURIComponent(id)}/versions?page=${page}`
    );
  }
  getResumeVersion(id: string, n: number) {
    return this.request<ResumeMaterialDto>(
      'GET',
      `materials/${encodeURIComponent(id)}/versions/${n}`
    );
  }
  restoreResumeVersion(id: string, n: number, etag: string) {
    return this.request<ResumeMaterialDto>(
      'POST',
      `materials/${encodeURIComponent(id)}/versions/${n}/restore`,
      undefined,
      undefined,
      false,
      { 'If-Match': etag }
    );
  }
  getResumeProposal(id: string, proposalId: string) {
    return this.request<ResumeProposalDto>(
      'GET',
      `materials/${encodeURIComponent(id)}/proposals/${encodeURIComponent(proposalId)}`
    );
  }
  applyResumeProposal(id: string, proposalId: string, acceptedChangeIds: string[], etag: string) {
    return this.request<ResumeMaterialDto>(
      'POST',
      `materials/${encodeURIComponent(id)}/proposals/${encodeURIComponent(proposalId)}/apply`,
      { acceptedChangeIds },
      undefined,
      false,
      { 'If-Match': etag }
    );
  }
  rejectResumeProposal(id: string, proposalId: string) {
    return this.request<unknown>(
      'POST',
      `materials/${encodeURIComponent(id)}/proposals/${encodeURIComponent(proposalId)}/reject`
    );
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

  getMarketMetrics() {
    return this.request<{ metrics: MarketMetric[] }>('GET', 'markets/metrics');
  }
  compareMarkets(query: MarketComparisonQuery) {
    const params = new URLSearchParams({ metric: query.metric, level: query.level });
    if (query.areas?.length) {
      params.set('areas', query.areas.join(','));
    }
    if (query.q) {
      params.set('q', query.q);
    }
    return this.request<MarketComparison>('GET', `markets/compare?${params.toString()}`);
  }
  /** Saves the area as the goal's location; needs the goal ETag from getGoal(). */
  saveMarketPreference(preference: { areaCode: string; level: MarketLevel }, goalEtag: string) {
    return this.request<CareerGoalDto>(
      'POST',
      'markets/preference',
      { ...preference, confirmed: true },
      'goal',
      false,
      { 'If-Match': goalEtag }
    );
  }
  getJobObservations(query: JobObservationsQuery = {}) {
    const params = new URLSearchParams();
    if (query.area) {
      params.set('area', query.area);
    }
    if (query.eligibleOnly) {
      params.set('eligibleOnly', 'true');
    }
    if (query.remote && query.remote !== 'all') {
      params.set('remote', query.remote);
    }
    if (query.q) {
      params.set('q', query.q);
    }
    const text = params.toString();
    return this.request<JobObservations>('GET', `jobs/observations${text ? `?${text}` : ''}`);
  }
  getJobSource() {
    return this.request<JobSourceInfo>('GET', 'jobs/source');
  }
}

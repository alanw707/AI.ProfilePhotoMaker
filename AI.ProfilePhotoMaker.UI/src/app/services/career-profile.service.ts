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
  provenance: Provenance;
  createdAt: string;
  updatedAt: string;
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
  source: 'resume' | 'pasted';
  resumeId: string | null;
  baseProfileVersion: number | null;
  status: 'pending' | 'accepted' | 'dismissed';
  isStale: boolean;
  items: ProposalItem[];
  createdAt: string;
}
export interface CareerApiError {
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
    match = false
  ): Observable<T> {
    const etag = resource === 'profile' ? this.profileEtag : this.goalEtag;
    const headers = match && etag ? new HttpHeaders({ 'If-Match': etag }) : new HttpHeaders();
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
    return {
      kind: kinds[error.status] ?? 'unknown',
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
}

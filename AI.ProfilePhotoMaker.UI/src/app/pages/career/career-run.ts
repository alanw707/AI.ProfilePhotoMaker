import { defer, repeat, retry, takeWhile, tap, throwError, timer, Observable } from 'rxjs';
import {
  CareerApiError,
  CareerProfileService,
  CareerRunDto,
  CareerRunStatus,
} from '../../services/career-profile.service';

/** Shared by every page that starts a run and follows it to the end. */
export const POLL_INTERVAL_MS = 2000;
export const MAX_BACKOFF_MS = 15000;
const ACTIVE: CareerRunStatus[] = ['queued', 'working', 'needs_input'];
const FATAL_KINDS: CareerApiError['kind'][] = ['notFound', 'unauthorized', 'disabled'];

export function isActive(status: CareerRunStatus) {
  return ACTIVE.includes(status);
}

/**
 * A poll sent before an answer or cancel can arrive after it. Keep whichever state of
 * the same run the server wrote last, so an old "needs your answer" never comes back.
 */
export function latestRun(current: CareerRunDto | null, incoming: CareerRunDto): CareerRunDto {
  if (!current || current.id !== incoming.id) {
    return incoming;
  }
  return Date.parse(incoming.updatedAt) < Date.parse(current.updatedAt) ? current : incoming;
}

export function backoffDelay(attempt: number) {
  return Math.min(POLL_INTERVAL_MS * 2 ** attempt, MAX_BACKOFF_MS);
}

/**
 * Polls a run until it stops being active. Network errors retry with backoff and are
 * reported through onConnection; notFound, unauthorized and disabled end the stream.
 */
export function pollRun(
  api: CareerProfileService,
  id: string,
  onConnection: (lost: boolean) => void
): Observable<CareerRunDto> {
  return defer(() => api.getRun(id)).pipe(
    retry({
      resetOnSuccess: true,
      delay: (e: CareerApiError, attempt) => {
        if (FATAL_KINDS.includes(e.kind)) {
          return throwError(() => e);
        }
        onConnection(true);
        return timer(backoffDelay(attempt - 1));
      },
    }),
    tap(() => onConnection(false)),
    repeat({ delay: POLL_INTERVAL_MS }),
    takeWhile(run => isActive(run.status), true)
  );
}

/** One key per user intent; survives a retry after a network error. */
export function startKey(storageKey: string): string {
  let key = sessionStorage.getItem(storageKey);
  if (!key) {
    key = crypto.randomUUID();
    sessionStorage.setItem(storageKey, key);
  }
  return key;
}

export function clearStartKey(storageKey: string) {
  sessionStorage.removeItem(storageKey);
}

/**
 * Only a lost connection may be retried with the same key; every other answer means
 * the next click is a new request.
 */
export function releaseStartKey(storageKey: string, error: CareerApiError) {
  if (error.kind !== 'unknown') {
    clearStartKey(storageKey);
  }
}

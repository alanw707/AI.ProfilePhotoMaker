import type {
  JobCoverageCounts,
  JobObservation,
  JobPay,
  RemoteEligibility,
} from '../../services/career-profile.service';

const WHOLE = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });
const CENTS = new Intl.NumberFormat('en-US', {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

export const REASON_COPY: Record<string, string> = {
  source_not_configured: 'The posting source is not configured yet.',
  source_unavailable:
    'The posting source could not be reached. The benchmark pages are unaffected.',
};
export const REASON_FALLBACK = 'Open postings are not available right now.';

const REMOTE_COPY: Record<RemoteEligibility, string> = {
  eligible: 'Remote work eligible',
  ineligible: 'Not remote eligible',
  unknown: 'Remote not stated',
};
export function remoteText(status: RemoteEligibility): string {
  return REMOTE_COPY[status] ?? REMOTE_COPY.unknown;
}

function money(pay: JobPay, value: number): string {
  return pay.unit === 'usd_per_hour' ? `$${CENTS.format(value)}` : `$${WHOLE.format(value)}`;
}

/** "$98,500 to $128,000 per year": the amounts as published, with the basis in words. */
export function payText(pay: JobPay): string {
  if (pay.status === 'not_available' || (pay.min === null && pay.max === null)) {
    return 'Pay not stated';
  }
  const basis = pay.unit === 'usd_per_hour' ? 'per hour' : 'per year';
  const [low, high] = [pay.min ?? pay.max, pay.max ?? pay.min] as [number, number];
  const range = low === high ? money(pay, low) : `${money(pay, low)} to ${money(pay, high)}`;
  return `${range}${pay.status === 'top_coded' ? ' or more' : ''} ${basis}`;
}

export function locationText(l: { city: string; state: string }): string {
  return [l.city, l.state].filter(Boolean).join(', ');
}

/** The location lines of an observation; a multi-location posting names the one near you. */
export function locationLines(o: JobObservation): { text: string; yours: boolean }[] {
  return o.locations.map(l => ({
    text: locationText(l),
    yours: o.multiLocation && l.match === 'user_area',
  }));
}

/** Plain-language lines for what the filters hid; zero counts are not listed. */
export function exclusionLines(c: JobCoverageCounts): string[] {
  const lines: [number, string][] = [
    [c.otherLocationExcluded, 'not open where you are'],
    [c.remoteUnknownExcluded, 'remote eligibility not stated'],
    [c.expired, 'already closed'],
    [c.duplicateIds + c.duplicateReposts, 'duplicates of a posting already shown'],
  ];
  return lines.filter(([n]) => n > 0).map(([n, why]) => `${n} hidden: ${why}`);
}

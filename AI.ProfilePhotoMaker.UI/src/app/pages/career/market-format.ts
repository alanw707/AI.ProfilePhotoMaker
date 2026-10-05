import type { MarketFigure } from '../../services/career-profile.service';

const MINUS = '\u2212';
const NUMBER = new Intl.NumberFormat('en-US', { maximumFractionDigits: 2 });
const WHOLE = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });
const CENTS = new Intl.NumberFormat('en-US', {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

/** Keys whose value is a difference, shown with an explicit sign. */
const SIGNED_KEYS = ['medianDifferenceAnnual'];

function amount(figure: MarketFigure, value: number): string {
  switch (figure.unit) {
    case 'usd_per_year':
      return `$${WHOLE.format(value)}`;
    case 'usd_per_hour':
      return `$${CENTS.format(value)}`;
    case 'jobs':
      return WHOLE.format(value);
    case 'jobs_thousands':
      return `${NUMBER.format(value)} thousand`;
    case 'per_1000_jobs':
      return `${NUMBER.format(value)} per 1,000 jobs`;
    case 'ratio':
      return CENTS.format(value);
    case 'percent':
      return `${NUMBER.format(value)}%`;
    case 'percent_rse':
      return `${NUMBER.format(value)}% relative standard error`;
    default:
      return NUMBER.format(value);
  }
}

/** The one place a published figure becomes display text. Never invents a number. */
export function formatFigure(figure: MarketFigure): string {
  switch (figure.status) {
    case 'not_available':
      return 'Not available (too few survey responses)';
    case 'not_published':
      return 'Not published for this occupation';
  }
  const value = figure.value;
  if (value === null || value === undefined) {
    return 'Not available';
  }
  if (typeof value === 'string' || figure.unit === 'text') {
    return String(value);
  }
  if (figure.status === 'top_coded') {
    return `${amount(figure, value)} or more`;
  }
  if (SIGNED_KEYS.includes(figure.key)) {
    const sign = value < 0 ? MINUS : '+';
    return `${sign}${amount(figure, Math.abs(value))}`;
  }
  return amount(figure, value);
}

export const NATIONAL_AREA = '99';

export interface ComparisonRow {
  key: string;
  label: string;
  national: MarketFigure | null;
  local: MarketFigure | null;
}
export interface ComparisonTable {
  nationalTitle: string;
  localTitle: string | null;
  rows: ComparisonRow[];
}

/** Groups figures by key into national and local columns, keeping the published order. */
export function comparisonRows(figures: MarketFigure[]): ComparisonTable {
  const rows = new Map<string, ComparisonRow>();
  let nationalTitle = 'National';
  let localTitle: string | null = null;
  for (const figure of figures) {
    const row = rows.get(figure.key) ?? {
      key: figure.key,
      label: figure.label,
      national: null,
      local: null,
    };
    if (figure.areaCode === NATIONAL_AREA) {
      row.national = figure;
      nationalTitle = figure.areaTitle;
    } else {
      row.local = figure;
      localTitle = figure.areaTitle;
    }
    rows.set(figure.key, row);
  }
  return { nationalTitle, localTitle, rows: [...rows.values()] };
}

const UNIT_LABELS: Record<string, string> = {
  usd_per_year: 'U.S. dollars per year',
  usd_per_hour: 'U.S. dollars per hour',
  jobs: 'number of jobs',
  ratio: 'ratio to the national level (1.00 means the same as the nation)',
  percent: 'percent',
};

/** Plain-language unit for a legend or table caption. */
export function unitLabel(unit: string): string {
  return UNIT_LABELS[unit] ?? unit;
}

const MONTH_NAMES = [
  'January',
  'February',
  'March',
  'April',
  'May',
  'June',
  'July',
  'August',
  'September',
  'October',
  'November',
  'December',
];

/** "2026-05-15" reads "15 May 2026"; anything that is not a valid ISO date stays as published. */
export function dateText(iso: string): string {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso);
  if (!match) {
    return iso;
  }
  const [year, month, day] = [Number(match[1]), Number(match[2]), Number(match[3])];
  const real = new Date(Date.UTC(year, month - 1, day));
  const valid =
    real.getUTCFullYear() === year && real.getUTCMonth() === month - 1 && real.getUTCDate() === day;
  return valid ? `${day} ${MONTH_NAMES[month - 1]} ${year}` : iso;
}

import {
  MarketFigure,
  MarketFigureStatus,
  MarketFigureUnit,
} from '../../services/career-profile.service';
import { comparisonRows, formatFigure } from './market-format';

function fig(
  unit: MarketFigureUnit,
  value: number | string | null,
  status: MarketFigureStatus = 'available',
  key = 'k'
): MarketFigure {
  return {
    key,
    label: 'L',
    value,
    status,
    unit,
    areaCode: '99',
    areaTitle: 'U.S.',
    sourceId: 'oews',
  };
}

describe('formatFigure', () => {
  const cases: [string, MarketFigure, string][] = [
    ['usd_per_year', fig('usd_per_year', 135980), '$135,980'],
    ['usd_per_year small', fig('usd_per_year', 980), '$980'],
    ['usd_per_hour', fig('usd_per_hour', 65.38), '$65.38'],
    ['usd_per_hour whole', fig('usd_per_hour', 65), '$65.00'],
    ['jobs', fig('jobs', 1687890), '1,687,890'],
    ['jobs_thousands', fig('jobs_thousands', 95.3), '95.3 thousand'],
    ['per_1000_jobs', fig('per_1000_jobs', 16.78), '16.78 per 1,000 jobs'],
    ['ratio', fig('ratio', 1.55), '1.55'],
    ['percent', fig('percent', 10.2), '10.2%'],
    ['percent negative', fig('percent', -3.4), '-3.4%'],
    ['percent_rse', fig('percent_rse', 0.4), '0.4% relative standard error'],
    ['text', fig('text', "Bachelor's degree"), "Bachelor's degree"],
    ['top_coded year', fig('usd_per_year', 239200, 'top_coded'), '$239,200 or more'],
    ['top_coded hour', fig('usd_per_hour', 115, 'top_coded'), '$115.00 or more'],
    ['top_coded jobs', fig('jobs', 1000, 'top_coded'), '1,000 or more'],
    [
      'not_available',
      fig('usd_per_year', null, 'not_available'),
      'Not available (too few survey responses)',
    ],
    [
      'not_available text',
      fig('text', null, 'not_available'),
      'Not available (too few survey responses)',
    ],
    ['not_published', fig('jobs', null, 'not_published'), 'Not published for this occupation'],
    ['available without value', fig('jobs', null), 'Not available'],
    [
      'positive difference',
      fig('usd_per_year', 1630, 'available', 'medianDifferenceAnnual'),
      '+$1,630',
    ],
    [
      'negative difference',
      fig('usd_per_year', -2000, 'available', 'medianDifferenceAnnual'),
      '\u2212$2,000',
    ],
    ['zero difference', fig('usd_per_year', 0, 'available', 'medianDifferenceAnnual'), '+$0'],
  ];
  for (const [name, figure, expected] of cases) {
    it(`formats ${name}`, () => expect(formatFigure(figure)).toBe(expected));
  }
});

describe('comparisonRows', () => {
  const at = (key: string, areaCode: string, value: number) => ({
    ...fig('usd_per_year', value, 'available', key),
    areaCode,
    areaTitle: areaCode === '99' ? 'U.S.' : 'Denver',
  });
  it('groups by key with national and local columns', () => {
    const table = comparisonRows([
      at('median', '99', 1),
      at('median', '19740', 2),
      at('mean', '99', 3),
      at('diff', '19740', 4),
    ]);
    expect(table.nationalTitle).toBe('U.S.');
    expect(table.localTitle).toBe('Denver');
    expect(table.rows.map(r => r.key)).toEqual(['median', 'mean', 'diff']);
    expect(table.rows[0].national?.value).toBe(1);
    expect(table.rows[0].local?.value).toBe(2);
    expect(table.rows[1].local).toBeNull();
    expect(table.rows[2].national).toBeNull();
  });
  it('has no local title without local figures', () => {
    expect(comparisonRows([at('median', '99', 1)]).localTitle).toBeNull();
  });
});

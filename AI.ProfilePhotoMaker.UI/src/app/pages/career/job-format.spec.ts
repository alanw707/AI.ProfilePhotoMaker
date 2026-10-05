import { exclusionLines, payText, remoteText } from './job-format';

describe('job-format', () => {
  const pay = (over: object) =>
    ({
      min: 98500,
      max: 128000,
      unit: 'usd_per_year',
      basis: 'annual',
      status: 'available',
      ...over,
    }) as never;
  it('states the pay basis in words and never invents pay', () => {
    expect(payText(pay({}))).toBe('$98,500 to $128,000 per year');
    expect(payText(pay({ min: 40, max: 55.5, unit: 'usd_per_hour' }))).toBe(
      '$40.00 to $55.50 per hour'
    );
    expect(payText(pay({ min: null, max: null, status: 'not_available' }))).toBe('Pay not stated');
    expect(payText(pay({ min: 200000, max: 200000, status: 'top_coded' }))).toBe(
      '$200,000 or more per year'
    );
  });
  it('never presents unknown remote as eligible', () => {
    expect(remoteText('unknown')).toBe('Remote not stated');
    expect(remoteText('eligible')).toBe('Remote work eligible');
  });
  it('lists only non-zero exclusions in plain words', () => {
    const counts = {
      matched: 9,
      shown: 5,
      duplicateIds: 1,
      duplicateReposts: 1,
      expired: 0,
      remoteUnknownExcluded: 0,
      otherLocationExcluded: 4,
    };
    expect(exclusionLines(counts)).toEqual([
      '4 hidden: not open where you are',
      '2 hidden: duplicates of a posting already shown',
    ]);
  });
});

import { runErrorMessage } from './career-run';
import { PLAIN_503 } from '../../services/career-profile.service';
import { actionLabel } from '../../admin/admin-career-usage/admin-career-usage.component';

describe('run start errors', () => {
  it('shows plain copy for paused, busy and cost cap', () => {
    expect(runErrorMessage({ kind: 'paused', message: PLAIN_503['paused'] as string })).toBe(
      'Drafting is paused for now. Your saved work is still available.'
    );
    expect(runErrorMessage({ kind: 'busy', message: 'x' })).toContain('try again in a minute');
    expect(runErrorMessage({ kind: 'costCap', message: 'x' })).toContain('saved work');
  });
  it('falls back to the server message, then a generic one', () => {
    expect(runErrorMessage({ kind: 'unknown', message: 'Nope' })).toBe('Nope');
    expect(runErrorMessage({ kind: 'unknown', message: '' })).toBe(
      'Something went wrong. Try again.'
    );
  });
  it('labels actions without machine codes', () => {
    expect(actionLabel('pay_analysis')).toBe('Pay analysis');
    expect(actionLabel('new_thing')).toBe('New thing');
  });
});

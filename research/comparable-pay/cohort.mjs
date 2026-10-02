export const RULE_VERSION = 'candidate-1.0';
export const LOOKBACK_DAYS = 90;
export const MIN_OBSERVATIONS = 10;
export const MIN_EMPLOYERS = 5;
export function quantile(values, p) {
  if (!values.length) return null;
  const sorted = [...values].sort((a,b) => a-b);
  const position = (sorted.length-1)*p, low = Math.floor(position), high = Math.ceil(position);
  return sorted[low] + (sorted[high]-sorted[low])*(position-low);
}
export function normalize(record, now = new Date()) {
  const reasons = [];
  if (!record.id || !record.employer || !record.role || !record.geography) reasons.push('missing identity or matching field');
  if (record.employerDisclosed !== true) reasons.push('not employer-disclosed pay');
  if (record.currency !== 'USD') reasons.push('unknown or non-USD currency');
  if (record.basis !== 'annual' && !(record.basis === 'hourly' && record.annualHours > 0)) reasons.push('unknown pay basis or annual hours');
  const age = (now - new Date(record.updatedAt)) / 86400000;
  if (!Number.isFinite(age) || age < 0 || age > LOOKBACK_DAYS) reasons.push('outside 90-day lookback');
  if (record.eligible === false) reasons.push('work-location ineligible');
  if (record.eligible == null) reasons.push('work-location eligibility unknown');
  const multiplier = record.basis === 'hourly' ? record.annualHours : 1;
  const low = Number(record.low)*multiplier, high = Number(record.high)*multiplier;
  if (!Number.isFinite(low) || !Number.isFinite(high) || low <= 0 || high < low) reasons.push('invalid pay range');
  return {record, low, high, reasons};
}
export function evaluate(records, query, now = new Date(), floor = {observations:MIN_OBSERVATIONS,employers:MIN_EMPLOYERS}) {
  const included = [], excluded = [], seen = new Set();
  for (const record of records) {
    const n = normalize(record, now), reasons = [...n.reasons];
    if (record.role !== query.role) reasons.push('different occupation family');
    if (record.geography !== query.geography) reasons.push('different geography');
    if (query.level && record.level !== query.level) reasons.push('different or unknown level');
    if (query.employmentType && record.employmentType !== query.employmentType) reasons.push('different or unknown employment type');
    const key = String(record.canonicalId || record.id);
    if (seen.has(key)) reasons.push('duplicate requisition');
    if (reasons.length) { excluded.push({id:record.id,reasons}); continue; }
    seen.add(key); included.push(n);
  }
  const employers = new Set(included.map(x => x.record.employer)).size;
  const employerCounts = {};
  for (const n of included) employerCounts[n.record.employer] = (employerCounts[n.record.employer] || 0) + 1;
  const concentration = included.length ? Math.max(...Object.values(employerCounts)) / included.length : 0;
  const supported = included.length >= floor.observations && employers >= floor.employers;
  const exclusionReasons = {};
  for (const row of excluded) for (const reason of row.reasons) exclusionReasons[reason] = (exclusionReasons[reason] || 0) + 1;
  return {rule:RULE_VERSION,role:query.role,geography:query.geography,level:query.level || 'any',
    included:included.length,employers,employerConcentration:Math.round(concentration*100)/100,
    excluded:excluded.length,exclusionReasons,
    interval:supported?{low:Math.round(quantile(included.map(x=>x.low),.25)),
      high:Math.round(quantile(included.map(x=>x.high),.75)),unit:'USD / year',
      definition:'P25 of advertised lower bounds to P75 of advertised upper bounds; linear interpolation'}:null,
    decision:supported?'observed interval':'occupational benchmark fallback',
    note:supported?'Employer-disclosed advertised pay, not an offer prediction.':'Insufficient independent current observations or employers.'};
}

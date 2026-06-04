export interface EthnicityOption {
  value: string;
  label: string;
  compatibilityOnly?: boolean;
}

export const LEGACY_GENERIC_ASIAN_VALUE = 'Asian';

export const ETHNICITY_OPTIONS: readonly EthnicityOption[] = [
  { value: 'East Asian', label: 'East Asian' },
  { value: 'South Asian', label: 'South Asian' },
  { value: 'Southeast Asian', label: 'Southeast Asian' },
  { value: 'Middle Eastern / North African', label: 'Middle Eastern / North African' },
  {
    value: 'Black / African / African American',
    label: 'Black / African / African American',
  },
  { value: 'Hispanic / Latino', label: 'Hispanic / Latino' },
  { value: 'Native American / Indigenous', label: 'Native American / Indigenous' },
  { value: 'Pacific Islander', label: 'Pacific Islander' },
  { value: 'White', label: 'White' },
  { value: 'Mixed / Multiracial', label: 'Mixed / Multiracial' },
  { value: 'Other', label: 'Other' },
  { value: 'Prefer not to say', label: 'Prefer not to say' },
] as const;

export const ETHNICITY_OPTIONS_WITH_LEGACY_GENERIC_ASIAN: readonly EthnicityOption[] = [
  {
    value: LEGACY_GENERIC_ASIAN_VALUE,
    label: 'Asian (general - existing profile)',
    compatibilityOnly: true,
  },
  ...ETHNICITY_OPTIONS,
] as const;

const exactValueMap = new Map(
  [...ETHNICITY_OPTIONS_WITH_LEGACY_GENERIC_ASIAN].map(option => [option.value, option.value])
);

const aliasEntries: readonly [string, string][] = [
  ['asian', LEGACY_GENERIC_ASIAN_VALUE],
  ['east-asian', 'East Asian'],
  ['south-asian', 'South Asian'],
  ['southeast-asian', 'Southeast Asian'],
  ['middle-eastern-north-african', 'Middle Eastern / North African'],
  ['middle-eastern', 'Middle Eastern / North African'],
  ['north-african', 'Middle Eastern / North African'],
  ['mena', 'Middle Eastern / North African'],
  ['black', 'Black / African / African American'],
  ['african', 'Black / African / African American'],
  ['african-american', 'Black / African / African American'],
  ['black-african-african-american', 'Black / African / African American'],
  ['hispanic', 'Hispanic / Latino'],
  ['latino', 'Hispanic / Latino'],
  ['latina', 'Hispanic / Latino'],
  ['latinx', 'Hispanic / Latino'],
  ['white', 'White'],
  ['caucasian', 'White'],
  ['native-american', 'Native American / Indigenous'],
  ['indigenous', 'Native American / Indigenous'],
  ['native-american-indigenous', 'Native American / Indigenous'],
  ['pacific-islander', 'Pacific Islander'],
  ['mixed', 'Mixed / Multiracial'],
  ['multiracial', 'Mixed / Multiracial'],
  ['mixed-multiracial', 'Mixed / Multiracial'],
  ['other', 'Other'],
  ['prefer-not-to-say', 'Prefer not to say'],
];

const slugAliasMap = new Map(aliasEntries);

function slugify(value: string): string {
  return value
    .toLowerCase()
    .replace(/&/g, ' and ')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '');
}

export function normalizeEthnicityValue(value?: string, includeLegacyGenericAsian = false): string {
  const normalized = (value || '').trim();
  if (!normalized) {
    return '';
  }

  const exactMatch = exactValueMap.get(normalized);
  if (exactMatch && (includeLegacyGenericAsian || exactMatch !== LEGACY_GENERIC_ASIAN_VALUE)) {
    return exactMatch;
  }

  const aliasMatch = slugAliasMap.get(slugify(normalized));
  if (!aliasMatch) {
    return '';
  }

  if (aliasMatch === LEGACY_GENERIC_ASIAN_VALUE && !includeLegacyGenericAsian) {
    return '';
  }

  return aliasMatch;
}

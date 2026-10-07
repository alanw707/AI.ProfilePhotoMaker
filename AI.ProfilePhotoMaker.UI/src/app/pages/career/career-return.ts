/**
 * Resolves the "back to career" link carried by the photo workspace (#391).
 *
 * The workspace URL carries a short key (never a URL) and a goal id. Only the keys below
 * are accepted, so a crafted link cannot send the user to another site.
 */
const CAREER_RETURN_TARGETS: Readonly<Record<string, { path: string; label: string }>> = {
  materials: { path: '/app/career/materials', label: 'career materials' },
  profile: { path: '/app/career/profile', label: 'career profile' },
  home: { path: '/app/career', label: 'career workspace' },
};

const GUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export interface CareerReturn {
  path: string;
  /** Destination name for "Back to <label>" copy. */
  label: string;
  goalId: string;
}

export function resolveCareerReturn(key: string | null, goal: string | null): CareerReturn | null {
  if (key === null || goal === null || !GUID_PATTERN.test(goal)) {
    return null;
  }
  // hasOwn keeps keys such as "constructor" or "__proto__" from resolving.
  if (!Object.prototype.hasOwnProperty.call(CAREER_RETURN_TARGETS, key)) {
    return null;
  }
  return { ...CAREER_RETURN_TARGETS[key], goalId: goal };
}

/** Query params for a link from career into the photo workspace. */
export function careerHandoffQuery(
  goalId: string | null | undefined,
  refineImageId?: number
): Record<string, string | number> {
  const query: Record<string, string | number> = {};
  if (refineImageId !== undefined) {
    query['refineImageId'] = refineImageId;
  }
  if (goalId) {
    query['careerReturn'] = 'materials';
    query['careerGoal'] = goalId;
  }
  return query;
}

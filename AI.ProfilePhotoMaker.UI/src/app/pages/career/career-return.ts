/**
 * Resolves the "back to career" link carried by the photo workspace (#391).
 *
 * The workspace URL carries a short key (never a URL) and a goal id. Only the keys below
 * are accepted, so a crafted link cannot send the user to another site.
 */
const CAREER_RETURN_PATHS: Readonly<Record<string, string>> = {
  materials: '/app/career/materials',
  profile: '/app/career/profile',
  home: '/app/career',
};

const GUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export interface CareerReturn {
  path: string;
  goalId: string;
}

export function resolveCareerReturn(key: string | null, goal: string | null): CareerReturn | null {
  if (key === null || goal === null || !GUID_PATTERN.test(goal)) {
    return null;
  }
  // hasOwn keeps keys such as "constructor" or "__proto__" from resolving.
  if (!Object.prototype.hasOwnProperty.call(CAREER_RETURN_PATHS, key)) {
    return null;
  }
  return { path: CAREER_RETURN_PATHS[key], goalId: goal };
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

import { careerHandoffQuery, resolveCareerReturn } from './career-return';

describe('resolveCareerReturn', () => {
  const goal = '3f2b8c1e-6a4d-4e0b-9d57-1c2a3b4c5d6e';

  it('maps the three allowed keys to fixed in-app paths', () => {
    expect(resolveCareerReturn('materials', goal)).toEqual({
      path: '/app/career/materials',
      goalId: goal,
    });
    expect(resolveCareerReturn('profile', goal)).toEqual({
      path: '/app/career/profile',
      goalId: goal,
    });
    expect(resolveCareerReturn('home', goal)).toEqual({ path: '/app/career', goalId: goal });
  });

  it('accepts an upper-case GUID', () => {
    expect(resolveCareerReturn('home', goal.toUpperCase())?.goalId).toBe(goal.toUpperCase());
  });

  const badKeys: (string | null)[] = [
    'https://evil.com',
    '//evil.com',
    '/\\evil.com',
    '\\\\evil.com',
    'javascript:alert(1)',
    'data:text/html,x',
    '/app/career/materials',
    '/app/career',
    'MATERIALS',
    'MATERIALS ',
    ' materials',
    'materials ',
    'materials/../x',
    'constructor',
    '__proto__',
    'toString',
    '',
    null,
  ];
  for (const key of badKeys) {
    it(`rejects key ${JSON.stringify(key)}`, () => {
      expect(resolveCareerReturn(key, goal)).toBeNull();
    });
  }

  const badGoals: (string | null)[] = [
    null,
    '',
    'abc',
    '42',
    ' ' + goal,
    goal + ' ',
    goal + 'x',
    goal.replace(/-/g, ''),
    goal.replace('3f2b', 'zzzz'),
    '../' + goal,
    goal + '&x=1',
  ];
  for (const badGoal of badGoals) {
    it(`rejects goal ${JSON.stringify(badGoal)}`, () => {
      expect(resolveCareerReturn('materials', badGoal)).toBeNull();
    });
  }
});

describe('careerHandoffQuery', () => {
  const goal = '3f2b8c1e-6a4d-4e0b-9d57-1c2a3b4c5d6e';

  it('builds the create link query', () => {
    expect(careerHandoffQuery(goal)).toEqual({ careerReturn: 'materials', careerGoal: goal });
  });

  it('adds the photo to improve', () => {
    expect(careerHandoffQuery(goal, 42)).toEqual({
      refineImageId: 42,
      careerReturn: 'materials',
      careerGoal: goal,
    });
  });

  it('carries no return when there is no goal', () => {
    expect(careerHandoffQuery(null)).toEqual({});
    expect(careerHandoffQuery(undefined, 42)).toEqual({ refineImageId: 42 });
  });
});

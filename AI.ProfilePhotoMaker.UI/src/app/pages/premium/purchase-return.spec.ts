import { workspaceReturnAfterPurchase } from './purchase-return';

describe('workspaceReturnAfterPurchase', () => {
  const goal = '3f2b8c1e-6a4d-4e0b-9d57-1c2a3b4c5d6e';

  it('returns to the workspace with every original query param plus the upgrade', () => {
    expect(
      workspaceReturnAfterPurchase(
        `/app/enhance?useCase=linkedin&previewId=7&careerReturn=materials&careerGoal=${goal}`,
        'starter_package'
      )
    ).toEqual({
      path: '/app/enhance',
      queryParams: {
        useCase: 'linkedin',
        previewId: '7',
        careerReturn: 'materials',
        careerGoal: goal,
        upgraded: 'starter_package',
      },
    });
  });

  it('ignores anything that is not a workspace return or has no package', () => {
    expect(
      workspaceReturnAfterPurchase('https://evil.com/app/enhance', 'starter_package')
    ).toBeNull();
    expect(workspaceReturnAfterPurchase('//evil.com', 'starter_package')).toBeNull();
    expect(workspaceReturnAfterPurchase('/app/career/materials', 'starter_package')).toBeNull();
    expect(workspaceReturnAfterPurchase('/app/enhance', null)).toBeNull();
    expect(workspaceReturnAfterPurchase(null, 'starter_package')).toBeNull();
  });
});

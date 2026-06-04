import { PortraitStyleCatalogModule } from './portrait-style-catalog';

interface TestCard {
  style: { name: string };
  key: string;
  group: 'recommended' | 'more' | 'fun';
  displayOrder: number;
  name: string;
}

describe('PortraitStyleCatalogModule', () => {
  const catalog = new PortraitStyleCatalogModule<TestCard>();
  const styles: TestCard[] = [
    {
      style: { name: 'linkedin' },
      key: 'linkedin',
      group: 'recommended',
      displayOrder: 10,
      name: 'LinkedIn',
    },
    {
      style: { name: 'executive' },
      key: 'executive',
      group: 'more',
      displayOrder: 20,
      name: 'Executive',
    },
    {
      style: { name: 'pixar_3d' },
      key: 'pixar-3d',
      group: 'fun',
      displayOrder: 30,
      name: 'Pixar 3D',
    },
  ];

  it('filters visible styles by group', () => {
    expect(catalog.getVisibleStyles(styles, 'fun').map(style => style.key)).toEqual(['pixar-3d']);
  });

  it('selects first style in a new group when current style is not visible there', () => {
    const result = catalog.selectGroup(styles, styles[0], 'more');
    expect(result?.group).toBe('more');
    expect(result?.selected?.key).toBe('executive');
  });

  it('restores preferred style before falling back to recommended', () => {
    expect(catalog.selectInitialStyle(styles, ['executive'])?.key).toBe('executive');
    expect(catalog.selectInitialStyle(styles, [null, undefined])?.key).toBe('linkedin');
  });

  it('selects a use-case recommendation by style name or key', () => {
    expect(
      catalog.selectRecommendedForUseCase(styles, { recommendedStyles: ['pixar-3d'] })?.style.name
    ).toBe('pixar_3d');
  });
});

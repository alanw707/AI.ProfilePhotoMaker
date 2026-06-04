export type PortraitStyleGroup = 'recommended' | 'more' | 'fun';

export interface PortraitStyleCatalogCard {
  style: { name: string };
  key: string;
  group: PortraitStyleGroup;
  displayOrder: number;
  name: string;
}

export interface PortraitStyleUseCase {
  recommendedStyles: string[];
}

/**
 * Deep Portrait style catalog module.
 *
 * Interface: callers provide catalog cards and ask catalog questions: visible group, group
 * existence, selected-group transition, restore/default selection, and use-case recommendation.
 * Implementation owns selection invariants so Photo workspace does not duplicate catalog rules.
 */
export class PortraitStyleCatalogModule<T extends PortraitStyleCatalogCard> {
  getVisibleStyles(styles: T[], group: PortraitStyleGroup): T[] {
    return styles.filter(style => style.group === group);
  }

  hasGroup(styles: T[], group: PortraitStyleGroup): boolean {
    return styles.some(style => style.group === group);
  }

  selectGroup(
    styles: T[],
    current: T | null,
    group: PortraitStyleGroup
  ): { selected: T | null; group: PortraitStyleGroup } | null {
    if (!this.hasGroup(styles, group)) {
      return null;
    }

    const visibleSelected = current?.group === group;
    return {
      group,
      selected: visibleSelected ? current : (this.getVisibleStyles(styles, group)[0] ?? current),
    };
  }

  selectInitialStyle(styles: T[], preferredNames: (string | null | undefined)[]): T | null {
    for (const name of preferredNames) {
      if (!name) {
        continue;
      }
      const match = this.findByStyleName(styles, name);
      if (match) {
        return match;
      }
    }

    return styles.find(style => style.group === 'recommended') ?? styles[0] ?? null;
  }

  selectRecommendedForUseCase(styles: T[], useCase: PortraitStyleUseCase): T | null {
    return (
      useCase.recommendedStyles
        .map(styleName =>
          styles.find(card => card.style.name === styleName || card.key === styleName)
        )
        .find((style): style is T => !!style) ?? null
    );
  }

  findByStyleName(styles: T[], styleName: string | null | undefined): T | null {
    if (!styleName) {
      return null;
    }

    return styles.find(card => card.style.name === styleName) ?? null;
  }
}

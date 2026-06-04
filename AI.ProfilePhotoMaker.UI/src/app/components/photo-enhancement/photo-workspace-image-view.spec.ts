import { PhotoWorkspaceImageViewModule } from './photo-workspace-image-view';

describe('PhotoWorkspaceImageViewModule', () => {
  const module = new PhotoWorkspaceImageViewModule({
    toApiImageUrl: path => `https://api.example.test${path}`,
  });

  it('keeps local data URLs as display URLs', () => {
    expect(module.normalizeDisplayImageUrl('data:image/png;base64,abc')).toBe(
      'data:image/png;base64,abc'
    );
  });

  it('prefers storage proxy URL for remote candidate URLs with storage paths', () => {
    expect(
      module.normalizeDisplayImageUrl('https://cdn.example.test/image.png', 'users/1/a.png')
    ).toBe('https://api.example.test/profile-images/users/1/a.png');
  });

  it('converts profile image paths through the API image adapter', () => {
    expect(module.normalizeDisplayImageUrl('/profile-images/users/1/a.png?size=small')).toBe(
      'https://api.example.test/profile-images/users/1/a.png?size=small'
    );
  });

  it('returns fallback image state before marking an image failed', () => {
    const first = module.nextFailedImageState({
      url: 'https://cdn.example.test/bad.png',
      displayUrl: 'https://cdn.example.test/bad.png',
      storagePath: 'users/1/a.png',
    });

    expect(first.displayUrl).toBe('https://api.example.test/profile-images/users/1/a.png');
    expect(first.fallbackAttempted).toBeTrue();
    expect(first.loadFailed).toBeFalsy();

    const second = module.nextFailedImageState(first);
    expect(second.loadFailed).toBeTrue();
  });
});

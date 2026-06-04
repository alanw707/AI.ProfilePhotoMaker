import { PhotoWorkspaceSessionModule } from './photo-workspace-session';

describe('PhotoWorkspaceSessionModule', () => {
  const session = new PhotoWorkspaceSessionModule();

  it('creates local file preview state with failed Before image reset', () => {
    expect(session.createLocalFilePreviewState('data:image/png;base64,abc')).toEqual({
      imagePreview: 'data:image/png;base64,abc',
      beforeImageLoadFailed: false,
    });
  });

  it('creates stored preview source state for resume and upgrade drafts', () => {
    expect(
      session.createStoredPreviewSourceState(
        'users/1/source.png',
        '/profile-images/users/1/source.png'
      )
    ).toEqual({
      imagePreview: '/profile-images/users/1/source.png',
      beforeImageLoadFailed: false,
      currentSourceStoragePath: 'users/1/source.png',
      previewSourceStoragePath: 'users/1/source.png',
      previewStyleName: null,
    });
  });

  it('creates a full cleared source state', () => {
    expect(session.createClearedSourceState()).toEqual({
      imagePreview: null,
      beforeImageLoadFailed: false,
      errorMessage: '',
      profileScore: null,
      generatedScore: null,
      qualityGateOverrideAccepted: false,
      currentSourceStoragePath: null,
      previewCandidate: null,
      previewSourceStoragePath: null,
      previewStyleName: null,
    });
  });
});

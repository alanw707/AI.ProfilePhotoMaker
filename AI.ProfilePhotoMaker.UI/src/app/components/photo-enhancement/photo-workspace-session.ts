export interface PhotoWorkspaceSourceState {
  imagePreview: string | null;
  beforeImageLoadFailed: boolean;
  currentSourceStoragePath: string | null;
  previewSourceStoragePath: string | null;
  previewStyleName: string | null;
}

export interface PhotoWorkspaceSessionResetState extends PhotoWorkspaceSourceState {
  errorMessage: string;
  profileScore: null;
  generatedScore: null;
  qualityGateOverrideAccepted: boolean;
  previewCandidate: null;
}

/**
 * Deep Photo workspace session module for source-preview state.
 *
 * Interface: callers announce a source transition (local file preview, restored stored preview,
 * or cleared source). Implementation owns the reset invariants that previously leaked across
 * upload, resume, upgrade-draft restore, and start-over paths.
 */
export class PhotoWorkspaceSessionModule {
  createLocalFilePreviewState(
    dataUrl: string
  ): Pick<PhotoWorkspaceSourceState, 'imagePreview' | 'beforeImageLoadFailed'> {
    return {
      imagePreview: dataUrl,
      beforeImageLoadFailed: false,
    };
  }

  createStoredPreviewSourceState(
    sourceStoragePath: string | null,
    displayUrl: string | null
  ): PhotoWorkspaceSourceState {
    return {
      imagePreview: displayUrl,
      beforeImageLoadFailed: false,
      currentSourceStoragePath: sourceStoragePath,
      previewSourceStoragePath: sourceStoragePath,
      previewStyleName: null,
    };
  }

  createClearedSourceState(): PhotoWorkspaceSessionResetState {
    return {
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
    };
  }
}

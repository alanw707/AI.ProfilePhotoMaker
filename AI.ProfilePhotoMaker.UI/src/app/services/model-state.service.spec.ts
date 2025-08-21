import { TestBed } from '@angular/core/testing';
import { ModelStateService } from './model-state.service';

describe('ModelStateService.getModelStatusFromData', () => {
  let service: ModelStateService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [ModelStateService],
    });
    service = TestBed.inject(ModelStateService);
  });

  it('returns "Model Ready" when latestTrainedModel exists', () => {
    const data = {
      hasTrainedModel: true,
      latestTrainedModel: { requestId: 'r3', trainedModelVersion: 'owner/model:ver' },
      allRequests: [
        { requestId: 'r1', status: 'creating', createdAt: '2025-08-01T00:00:00Z' },
        { requestId: 'r2', status: 'failed', createdAt: '2025-08-02T00:00:00Z' },
        { requestId: 'r3', status: 'ready', createdAt: '2025-08-03T00:00:00Z' },
      ],
    };

    const result = service.getModelStatusFromData(data, null);
    expect(result.modelStatus).toBe('Model Ready');
    expect(result.hasTrainedModel).toBeTrue();
    expect(result.latestTrainedModel).toBeTruthy();
  });

  it('uses only the most recent request to decide training state', () => {
    const data = {
      hasTrainedModel: false,
      latestTrainedModel: null,
      allRequests: [
        // Old pending/creating request should not force training
        { requestId: 'old', status: 'creating', createdAt: '2025-07-01T00:00:00Z' },
        // Latest is failed, so not training
        { requestId: 'latest', status: 'failed', createdAt: '2025-08-03T00:00:00Z' },
      ],
    };

    const result = service.getModelStatusFromData(data, { status: 'Not Started' });
    expect(result.modelStatus).not.toBe('training');
  });

  it('returns training when the latest request is creating or pending', () => {
    const data = {
      hasTrainedModel: false,
      latestTrainedModel: null,
      allRequests: [
        { requestId: 'older', status: 'failed', createdAt: '2025-08-01T00:00:00Z' },
        { requestId: 'latest', status: 'creating', createdAt: '2025-08-05T00:00:00Z' },
      ],
    };

    const result = service.getModelStatusFromData(data, null);
    expect(result.modelStatus).toBe('training');
  });

  it('returns "Ready for training" when a model was deleted from Replicate', () => {
    const data = {
      hasTrainedModel: false,
      latestTrainedModel: null,
      allRequests: [
        {
          requestId: 'r1',
          status: 'failed',
          createdAt: '2025-08-05T00:00:00Z',
          errorMessage: 'Model was deleted from Replicate externally',
        },
      ],
    };

    const result = service.getModelStatusFromData(data, null);
    expect(result.modelStatus).toBe('Ready for training');
  });
});

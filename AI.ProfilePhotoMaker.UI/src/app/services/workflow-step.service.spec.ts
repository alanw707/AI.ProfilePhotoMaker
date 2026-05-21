import { WorkflowStepService } from './workflow-step.service';

describe('WorkflowStepService', () => {
  let service: WorkflowStepService;

  beforeEach(() => {
    service = new WorkflowStepService();
  });

  it('keeps step 1 active when fewer than 5 images are uploaded', () => {
    const status = service.getStepStatus(1, 1, [{ id: '1', name: 'photo-1' }], 0, 1);
    const nextStep = service.updateCurrentStep(1, [{ id: '1', name: 'photo-1' }], 0, 1);

    expect(status).toBe('active');
    expect(nextStep).toBe(1);
  });

  it('marks step 1 complete and advances to step 2 at 5 images', () => {
    const thumbnails = Array.from({ length: 5 }, (_, index) => ({
      id: `${index + 1}`,
      name: `photo-${index + 1}`,
    }));

    const status = service.getStepStatus(1, 5, thumbnails, 0, 1);
    const nextStep = service.updateCurrentStep(5, thumbnails, 0, 1);

    expect(status).toBe('completed');
    expect(nextStep).toBe(2);
  });
});

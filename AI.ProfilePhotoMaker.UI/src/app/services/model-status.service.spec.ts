import { TestBed } from '@angular/core/testing';
import { ModelStatusService } from './model-status.service';

describe('ModelStatusService', () => {
  let service: ModelStatusService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(ModelStatusService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  describe('canGenerate', () => {
    it('should return true for unified status codes', () => {
      expect(service.canGenerate('ModelReady')).toBe(true);
    });

    it('should return true for legacy display strings', () => {
      expect(service.canGenerate('Model Ready')).toBe(true);
      expect(service.canGenerate('Model trained - ready for generation')).toBe(true);
    });

    it('should return false for non-ready statuses', () => {
      expect(service.canGenerate('Training')).toBe(false);
      expect(service.canGenerate('Not Started')).toBe(false);
      expect(service.canGenerate('')).toBe(false);
      expect(service.canGenerate(null as any)).toBe(false);
    });

    it('should handle defensive status arrays correctly', () => {
      // These were the problematic defensive arrays being replaced
      const defensiveStatuses = ['Model Ready', 'Ready', 'Done', 'Completed', 'ModelReady'];

      defensiveStatuses.forEach(status => {
        const result = service.canGenerate(status);
        expect(result).toBe(
          true,
          `Status "${status}" should be recognized as ready for generation`
        );
      });
    });
  });

  describe('isTraining', () => {
    it('should return true for training statuses', () => {
      expect(service.isTraining('Training')).toBe(true);
      expect(service.isTraining('training')).toBe(true);
      expect(service.isTraining('creating')).toBe(true);
      expect(service.isTraining('pending')).toBe(true);
    });

    it('should return false for non-training statuses', () => {
      expect(service.isTraining('ModelReady')).toBe(false);
      expect(service.isTraining('Model Ready')).toBe(false);
      expect(service.isTraining('Not Started')).toBe(false);
    });
  });

  describe('getStatusInfo', () => {
    it('should provide comprehensive status information', () => {
      const readyInfo = service.getStatusInfo('ModelReady');
      expect(readyInfo.canGenerate).toBe(true);
      expect(readyInfo.isTraining).toBe(false);
      expect(readyInfo.actionable).toBe(true);
      expect(readyInfo.displayText).toBe('Model Ready');

      const trainingInfo = service.getStatusInfo('Training');
      expect(trainingInfo.canGenerate).toBe(false);
      expect(trainingInfo.isTraining).toBe(true);
      expect(trainingInfo.actionable).toBe(false);
      expect(trainingInfo.displayText).toBe('Training...');
    });
  });

  describe('complexity reduction validation', () => {
    it('should eliminate need for defensive status arrays', () => {
      // Previously: ['Model Ready', 'Ready', 'Done', 'Completed', 'ModelReady'].some(status => ...)
      // Now: service.canGenerate(status)

      const complexDefensiveLogic = (status: string) =>
        ['Model Ready', 'Ready', 'Done', 'Completed', 'ModelReady'].some(s => status === s);

      const simplifiedLogic = (status: string) => service.canGenerate(status);

      // Test cases should produce same results
      expect(simplifiedLogic('Model Ready')).toBe(complexDefensiveLogic('Model Ready'));
      expect(simplifiedLogic('ModelReady')).toBe(complexDefensiveLogic('ModelReady'));
      expect(simplifiedLogic('Training')).toBe(complexDefensiveLogic('Training'));
      expect(simplifiedLogic('Not Started')).toBe(complexDefensiveLogic('Not Started'));
    });
  });
});

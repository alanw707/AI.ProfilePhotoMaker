import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { StyleOption, StyleSelectorComponent } from './style-selector.component';
import { FormsModule } from '@angular/forms';
import { RouterTestingModule } from '@angular/router/testing';
import { CommonModule } from '@angular/common';

describe('StyleSelectorComponent', () => {
  let component: StyleSelectorComponent;
  let fixture: ComponentFixture<StyleSelectorComponent>;
  let mockRouter: jasmine.SpyObj<Router>;

  beforeEach(async () => {
    const routerSpy = jasmine.createSpyObj('Router', ['navigate']);

    await TestBed.configureTestingModule({
      imports: [
        StyleSelectorComponent,
        CommonModule,
        FormsModule,
        RouterTestingModule
      ],
      providers: [
        { provide: Router, useValue: routerSpy }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(StyleSelectorComponent);
    component = fixture.componentInstance;
    mockRouter = TestBed.inject(Router) as jasmine.SpyObj<Router>;
    fixture.detectChanges();
  });

  describe('Component Initialization', () => {
    it('should create', () => {
      expect(component).toBeTruthy();
    });

    it('should have correct default input values', () => {
      expect(component.availableStyles).toEqual([]);
      expect(component.imagesPerStyle).toBe(2);
      expect(component.selectedStyles).toBe(0);
      expect(component.trainingCredits).toBe(15);
      expect(component.generationCredits).toBe(0);
      expect(component.totalCredits).toBe(0);
      expect(component.hasEnoughCredits).toBeTrue();
      expect(component.remainingCredits).toBe(0);
      expect(component.isTrainingStarted).toBeFalse();
      expect(component.modelStatus).toBe('');
      expect(component.uploadedImageCount).toBe(0);
      expect(component.isTraining).toBeFalse();
      expect(component.isGenerating).toBeFalse();
      expect(component.progressPercentage).toBe(0);
      expect(component.progressMessage).toBe('');
      expect(component.estimatedCompletion).toBe('');
      expect(component.lastGenerationCount).toBe(0);
      expect(component.showLastGenerationMessage).toBeFalse();
    });
  });

  describe('Event Emission', () => {
    it('should emit styleToggled event when style is toggled', () => {
      const mockStyle: StyleOption = {
        id: 'business',
        name: 'Business',
        description: 'Professional business style',
        previewUrl: 'preview.jpg',
        selected: false
      };
      spyOn(component.styleToggled, 'emit');

      component.onToggleStyle(mockStyle);

      expect(component.styleToggled.emit).toHaveBeenCalledWith(mockStyle);
    });

    it('should emit imagesPerStyleChanged event when images per style changes', () => {
      spyOn(component.imagesPerStyleChanged, 'emit');
      const newCount = 3;

      component.onImagesPerStyleChange(newCount);

      expect(component.imagesPerStyle).toBe(newCount);
      expect(component.imagesPerStyleChanged.emit).toHaveBeenCalledWith(newCount);
    });

    it('should emit selectAllStyles event when select all is clicked', () => {
      spyOn(component.selectAllStyles, 'emit');

      component.onSelectAll();

      expect(component.selectAllStyles.emit).toHaveBeenCalled();
    });

    it('should emit deselectAllStyles event when deselect all is clicked', () => {
      spyOn(component.deselectAllStyles, 'emit');

      component.onDeselectAll();

      expect(component.deselectAllStyles.emit).toHaveBeenCalled();
    });

    it('should emit startTraining event when training is started', () => {
      spyOn(component.startTraining, 'emit');

      component.onStartTraining();

      expect(component.startTraining.emit).toHaveBeenCalled();
    });

    it('should emit continueInBackground event when continue in background is clicked', () => {
      spyOn(component.continueInBackground, 'emit');

      component.onContinueInBackground();

      expect(component.continueInBackground.emit).toHaveBeenCalled();
    });

    it('should emit dismissSuccessMessage event when success message is dismissed', () => {
      spyOn(component.dismissSuccessMessage, 'emit');

      component.onDismissSuccessMessage();

      expect(component.dismissSuccessMessage.emit).toHaveBeenCalled();
    });
  });

  describe('Style Name Formatting', () => {
    it('should format style names correctly', () => {
      expect(component.formatStyleName('business-professional')).toBe('Business Professional');
      expect(component.formatStyleName('casual-headshot')).toBe('Casual Headshot');
      expect(component.formatStyleName('corporate')).toBe('Corporate');
      expect(component.formatStyleName('linkedin-profile')).toBe('Linkedin Profile');
    });

    it('should handle empty or undefined style names', () => {
      expect(component.formatStyleName('')).toBe('');
      expect(component.formatStyleName(undefined as any)).toBe('');
      expect(component.formatStyleName(null as any)).toBe('');
    });

    it('should handle style names with multiple dashes', () => {
      expect(component.formatStyleName('business-professional-headshot')).toBe('Business Professional Headshot');
      expect(component.formatStyleName('casual-outdoor-portrait')).toBe('Casual Outdoor Portrait');
    });

    it('should handle style names with mixed case', () => {
      expect(component.formatStyleName('BUSINESS-PROFESSIONAL')).toBe('Business Professional');
      expect(component.formatStyleName('casual-HEADSHOT')).toBe('Casual Headshot');
    });
  });

  describe('Image Error Handling', () => {
    it('should handle image load errors', () => {
      const mockEvent = {
        target: {
          src: 'original-url.jpg',
          onerror: jasmine.createSpy('onerror')
        }
      };

      component.onImageError(mockEvent);

      expect(mockEvent.target.src).toBe('/api/placeholder/style-preview');
      expect(mockEvent.target.onerror).toBeNull();
    });

    it('should set fallback image source on error', () => {
      const mockTarget = {
        src: 'broken-image.jpg',
        onerror: () => {}
      };
      const mockEvent = { target: mockTarget };

      component.onImageError(mockEvent);

      expect(mockTarget.src).toBe('/api/placeholder/style-preview');
    });
  });

  describe('Navigation', () => {
    it('should navigate to gallery with refresh parameter', () => {
      const expectedParams = {
        queryParams: { refresh: jasmine.any(Number) }
      };

      component.navigateToGallery();

      expect(mockRouter.navigate).toHaveBeenCalledWith(['/gallery'], expectedParams);
    });

    it('should navigate to gallery with current timestamp', () => {
      const dateSpy = spyOn(Date, 'now').and.returnValue(12345);

      component.navigateToGallery();

      expect(mockRouter.navigate).toHaveBeenCalledWith(['/gallery'], {
        queryParams: { refresh: 12345 }
      });
    });
  });

  describe('Input Properties Validation', () => {
    it('should handle availableStyles array', () => {
      const mockStyles: StyleOption[] = [
        {
          id: 'business',
          name: 'Business',
          description: 'Professional business style',
          previewUrl: 'business.jpg',
          selected: false
        },
        {
          id: 'casual',
          name: 'Casual',
          description: 'Casual headshot style',
          previewUrl: 'casual.jpg',
          selected: true
        }
      ];

      component.availableStyles = mockStyles;

      expect(component.availableStyles).toEqual(mockStyles);
      expect(component.availableStyles.length).toBe(2);
    });

    it('should handle numeric input properties', () => {
      component.imagesPerStyle = 4;
      component.selectedStyles = 3;
      component.trainingCredits = 20;
      component.generationCredits = 15;
      component.totalCredits = 35;
      component.remainingCredits = 20;
      component.uploadedImageCount = 10;
      component.progressPercentage = 75;
      component.lastGenerationCount = 8;

      expect(component.imagesPerStyle).toBe(4);
      expect(component.selectedStyles).toBe(3);
      expect(component.trainingCredits).toBe(20);
      expect(component.generationCredits).toBe(15);
      expect(component.totalCredits).toBe(35);
      expect(component.remainingCredits).toBe(20);
      expect(component.uploadedImageCount).toBe(10);
      expect(component.progressPercentage).toBe(75);
      expect(component.lastGenerationCount).toBe(8);
    });

    it('should handle boolean input properties', () => {
      component.hasEnoughCredits = false;
      component.isTrainingStarted = true;
      component.isTraining = true;
      component.isGenerating = true;
      component.showLastGenerationMessage = true;

      expect(component.hasEnoughCredits).toBeFalse();
      expect(component.isTrainingStarted).toBeTrue();
      expect(component.isTraining).toBeTrue();
      expect(component.isGenerating).toBeTrue();
      expect(component.showLastGenerationMessage).toBeTrue();
    });

    it('should handle string input properties', () => {
      component.modelStatus = 'training';
      component.progressMessage = 'Processing images...';
      component.estimatedCompletion = '5 minutes';

      expect(component.modelStatus).toBe('training');
      expect(component.progressMessage).toBe('Processing images...');
      expect(component.estimatedCompletion).toBe('5 minutes');
    });
  });

  describe('Edge Cases', () => {
    it('should handle zero values correctly', () => {
      component.imagesPerStyle = 0;
      component.selectedStyles = 0;
      component.trainingCredits = 0;
      component.generationCredits = 0;
      component.totalCredits = 0;
      component.remainingCredits = 0;
      component.uploadedImageCount = 0;
      component.progressPercentage = 0;
      component.lastGenerationCount = 0;

      expect(component.imagesPerStyle).toBe(0);
      expect(component.selectedStyles).toBe(0);
      expect(component.trainingCredits).toBe(0);
      expect(component.generationCredits).toBe(0);
      expect(component.totalCredits).toBe(0);
      expect(component.remainingCredits).toBe(0);
      expect(component.uploadedImageCount).toBe(0);
      expect(component.progressPercentage).toBe(0);
      expect(component.lastGenerationCount).toBe(0);
    });

    it('should handle negative values appropriately', () => {
      component.imagesPerStyle = -1;
      component.selectedStyles = -2;
      component.remainingCredits = -5;

      expect(component.imagesPerStyle).toBe(-1);
      expect(component.selectedStyles).toBe(-2);
      expect(component.remainingCredits).toBe(-5);
    });

    it('should handle very large values', () => {
      component.imagesPerStyle = 1000;
      component.trainingCredits = 999999;
      component.progressPercentage = 100;

      expect(component.imagesPerStyle).toBe(1000);
      expect(component.trainingCredits).toBe(999999);
      expect(component.progressPercentage).toBe(100);
    });

    it('should handle empty strings', () => {
      component.modelStatus = '';
      component.progressMessage = '';
      component.estimatedCompletion = '';

      expect(component.modelStatus).toBe('');
      expect(component.progressMessage).toBe('');
      expect(component.estimatedCompletion).toBe('');
    });
  });

  describe('Integration Tests', () => {
    it('should handle complete style selection workflow', () => {
      const mockStyle: StyleOption = {
        id: 'business',
        name: 'Business',
        description: 'Professional business style',
        previewUrl: 'business.jpg',
        selected: false
      };

      spyOn(component.styleToggled, 'emit');
      spyOn(component.imagesPerStyleChanged, 'emit');
      spyOn(component.startTraining, 'emit');

      // Select style
      component.onToggleStyle(mockStyle);
      expect(component.styleToggled.emit).toHaveBeenCalledWith(mockStyle);

      // Change images per style
      component.onImagesPerStyleChange(3);
      expect(component.imagesPerStyleChanged.emit).toHaveBeenCalledWith(3);

      // Start training
      component.onStartTraining();
      expect(component.startTraining.emit).toHaveBeenCalled();
    });

    it('should handle training progress and completion workflow', () => {
      spyOn(component.continueInBackground, 'emit');
      spyOn(component.dismissSuccessMessage, 'emit');

      // Set training state
      component.isTraining = true;
      component.progressPercentage = 50;
      component.progressMessage = 'Training in progress...';

      // Continue in background
      component.onContinueInBackground();
      expect(component.continueInBackground.emit).toHaveBeenCalled();

      // Complete training
      component.isTraining = false;
      component.showLastGenerationMessage = true;
      component.lastGenerationCount = 5;

      // Dismiss success message
      component.onDismissSuccessMessage();
      expect(component.dismissSuccessMessage.emit).toHaveBeenCalled();
    });
  });
});
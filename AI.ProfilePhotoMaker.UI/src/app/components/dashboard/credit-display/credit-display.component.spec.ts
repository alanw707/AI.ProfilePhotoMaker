import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { CreditDisplayComponent, CreditInfo, UserCreditStatus } from './credit-display.component';

describe('CreditDisplayComponent', () => {
  let component: CreditDisplayComponent;
  let fixture: ComponentFixture<CreditDisplayComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CreditDisplayComponent, RouterTestingModule],
    }).compileComponents();

    fixture = TestBed.createComponent(CreditDisplayComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('Credit Calculation Methods', () => {
    it('should calculate total available credits from user status', () => {
      component.userCreditStatus = {
        credits: 15,
      };

      expect(component.getTotalAvailableCredits()).toBe(15);
    });

    it('should fallback to creditsInfo when userCreditStatus is null', () => {
      component.userCreditStatus = null;
      component.creditsInfo = {
        credits: 3,
      };

      expect(component.getTotalAvailableCredits()).toBe(3);
    });

    it('should return 0 when no credit information is available', () => {
      component.userCreditStatus = null;
      component.creditsInfo = null;

      expect(component.getTotalAvailableCredits()).toBe(0);
    });
  });

  describe('Display Text Methods', () => {
    it('should generate correct display text for available credits', () => {
      component.userCreditStatus = {
        credits: 13,
      };

      expect(component.getCreditDisplayText()).toBe('13 Credits');
    });

    it('should generate correct display text for no credits', () => {
      component.userCreditStatus = {
        credits: 0,
      };

      expect(component.getCreditDisplayText()).toBe('0 Credits');
    });

    it('should generate correct display text in headshot context', () => {
      component.displayContext = 'headshots';
      component.userCreditStatus = {
        credits: 8,
      };

      expect(component.getCreditDisplayText()).toBe('8 Credits');
    });
  });

  describe('Subtitle Text Methods', () => {
    it('should show correct subtitle for available credits', () => {
      component.userCreditStatus = {
        credits: 13,
      };

      expect(component.getCreditSubtitleText()).toBe(
        'Use credits for training, generation, and enhancement'
      );
    });

    it('should show correct subtitle for no credits', () => {
      component.userCreditStatus = {
        credits: 0,
      };

      expect(component.getCreditSubtitleText()).toBe('Purchase credits to get started');
    });

    it('should show correct subtitle in headshot context', () => {
      component.displayContext = 'headshots';
      component.userCreditStatus = {
        credits: 5,
      };

      expect(component.getCreditSubtitleText()).toBe(
        'Credits available for training and generation'
      );
    });
  });

  describe('Purchase Prompt Logic', () => {
    it('should show purchase prompt when no credits are available', () => {
      component.userCreditStatus = {
        credits: 0,
      };

      expect(component.shouldShowPurchasePrompt()).toBe(true);
    });

    it('should show purchase prompt when required credits exceed balance', () => {
      component.userCreditStatus = {
        credits: 2,
      };
      component.requiredCredits = 5;

      expect(component.shouldShowPurchasePrompt()).toBe(true);
    });

    it('should not show purchase prompt when credits are sufficient', () => {
      component.userCreditStatus = {
        credits: 10,
      };
      component.requiredCredits = 5;

      expect(component.shouldShowPurchasePrompt()).toBe(false);
    });
  });

  describe('Insufficient Credits Warning', () => {
    it('should show warning when required credits exceed available credits', () => {
      component.userCreditStatus = {
        credits: 5,
      };
      component.requiredCredits = 10;
      component.hasEnoughCredits = false;

      expect(component.shouldShowInsufficientCreditsWarning()).toBe(true);
    });

    it('should not show warning when user has enough credits', () => {
      component.userCreditStatus = {
        credits: 10,
      };
      component.requiredCredits = 5;
      component.hasEnoughCredits = true;

      expect(component.shouldShowInsufficientCreditsWarning()).toBe(false);
    });
  });

  describe('Icon Selection', () => {
    it('should return diamond icon for available credits', () => {
      component.userCreditStatus = {
        credits: 3,
      };

      expect(component.getCreditIcon()).toBe('💎');
    });

    it('should return warning icon for no credits', () => {
      component.userCreditStatus = {
        credits: 0,
      };

      expect(component.getCreditIcon()).toBe('⚠️');
    });
  });

  describe('Event Emission', () => {
    it('should emit creditActionRequested event on purchase action', () => {
      spyOn(component.creditActionRequested, 'emit');

      component.onCreditAction('purchase', 'test-context');

      expect(component.creditActionRequested.emit).toHaveBeenCalledWith({
        action: 'purchase',
        context: 'test-context',
      });
    });

    it('should emit creditActionRequested event on view packages action', () => {
      spyOn(component.creditActionRequested, 'emit');

      component.onCreditAction('viewPackages');

      expect(component.creditActionRequested.emit).toHaveBeenCalledWith({
        action: 'viewPackages',
        context: undefined,
      });
    });

    it('should emit creditActionRequested event on upgrade action', () => {
      spyOn(component.creditActionRequested, 'emit');

      component.onCreditAction('upgrade', 'premium-context');

      expect(component.creditActionRequested.emit).toHaveBeenCalledWith({
        action: 'upgrade',
        context: 'premium-context',
      });
    });
  });

  describe('Input Properties', () => {
    it('should have correct default values for all input properties', () => {
      expect(component.creditsInfo).toBeNull();
      expect(component.userCreditStatus).toBeNull();
      expect(component.isLoading).toBeFalse();
      expect(component.showCard).toBeTrue();
      expect(component.showSettingsHint).toBeFalse();
      expect(component.showBreakdown).toBeFalse();
      expect(component.showPurchasePrompt).toBeFalse();
      expect(component.requiredCredits).toBe(0);
      expect(component.trainingCredits).toBe(0);
      expect(component.generationCredits).toBe(0);
      expect(component.totalCredits).toBe(0);
      expect(component.hasEnoughCredits).toBeTrue();
      expect(component.remainingCredits).toBe(0);
      expect(component.displayContext).toBe('default');
    });

    it('should handle undefined userCreditStatus properties', () => {
      component.userCreditStatus = {} as UserCreditStatus;

      expect(component.getTotalAvailableCredits()).toBe(0);
    });

    it('should handle undefined creditsInfo properties', () => {
      component.creditsInfo = {} as CreditInfo;
      component.userCreditStatus = null;

      expect(component.getTotalAvailableCredits()).toBe(0);
    });
  });

  describe('Edge Cases', () => {
    it('should handle required credits when no data is available', () => {
      component.userCreditStatus = null;
      component.creditsInfo = null;
      component.requiredCredits = 10;

      expect(component.shouldShowPurchasePrompt()).toBe(true);
    });
  });
});

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { CreditDisplayComponent, CreditInfo, UserCreditStatus } from './credit-display.component';

describe('CreditDisplayComponent', () => {
  let component: CreditDisplayComponent;
  let fixture: ComponentFixture<CreditDisplayComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CreditDisplayComponent, RouterTestingModule]
    })
    .compileComponents();
    
    fixture = TestBed.createComponent(CreditDisplayComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('Credit Calculation Methods', () => {
    it('should calculate total available credits correctly', () => {
      component.userCreditStatus = {
        weeklyCredits: 5,
        purchasedCredits: 10
      };
      
      expect(component.getTotalAvailableCredits()).toBe(15);
    });

    it('should get purchased credits correctly', () => {
      component.userCreditStatus = {
        weeklyCredits: 5,
        purchasedCredits: 10
      };
      
      expect(component.getPurchasedCredits()).toBe(10);
    });

    it('should get weekly credits correctly', () => {
      component.userCreditStatus = {
        weeklyCredits: 5,
        purchasedCredits: 10
      };
      
      expect(component.getWeeklyCredits()).toBe(5);
    });

    it('should fallback to creditsInfo when userCreditStatus is null', () => {
      component.userCreditStatus = null;
      component.creditsInfo = {
        availableCredits: 3
      };
      
      expect(component.getWeeklyCredits()).toBe(3);
      expect(component.getTotalAvailableCredits()).toBe(3);
    });

    it('should return 0 when no credit information is available', () => {
      component.userCreditStatus = null;
      component.creditsInfo = null;
      
      expect(component.getWeeklyCredits()).toBe(0);
      expect(component.getPurchasedCredits()).toBe(0);
      expect(component.getTotalAvailableCredits()).toBe(0);
    });
  });

  describe('Display Text Methods', () => {
    it('should generate correct display text for mixed credits', () => {
      component.userCreditStatus = {
        weeklyCredits: 3,
        purchasedCredits: 10
      };
      
      expect(component.getCreditDisplayText()).toBe('13 Credits (3 Weekly + 10 Purchased)');
    });

    it('should generate correct display text for purchased credits only', () => {
      component.userCreditStatus = {
        weeklyCredits: 0,
        purchasedCredits: 10
      };
      
      expect(component.getCreditDisplayText()).toBe('10 Purchased Credits');
    });

    it('should generate correct display text for weekly credits only', () => {
      component.userCreditStatus = {
        weeklyCredits: 3,
        purchasedCredits: 0
      };
      
      expect(component.getCreditDisplayText()).toBe('3 Weekly Credits');
    });

    it('should generate correct display text for no credits', () => {
      component.userCreditStatus = {
        weeklyCredits: 0,
        purchasedCredits: 0
      };
      
      expect(component.getCreditDisplayText()).toBe('0 Credits');
    });
  });

  describe('Subtitle Text Methods', () => {
    it('should show correct subtitle for mixed credits', () => {
      component.userCreditStatus = {
        weeklyCredits: 3,
        purchasedCredits: 10
      };
      
      expect(component.getCreditSubtitleText()).toBe('For model training and generation');
    });

    it('should show correct subtitle for purchased credits only', () => {
      component.userCreditStatus = {
        weeklyCredits: 0,
        purchasedCredits: 10
      };
      
      expect(component.getCreditSubtitleText()).toBe('For premium features');
    });

    it('should show correct subtitle for weekly credits only', () => {
      component.userCreditStatus = {
        weeklyCredits: 3,
        purchasedCredits: 0
      };
      
      expect(component.getCreditSubtitleText()).toBe('For basic photo enhancement');
    });

    it('should show correct subtitle for no credits', () => {
      component.userCreditStatus = {
        weeklyCredits: 0,
        purchasedCredits: 0
      };
      
      expect(component.getCreditSubtitleText()).toBe('Purchase credits to get started');
    });
  });

  describe('Purchase Prompt Logic', () => {
    it('should show purchase prompt when no purchased credits and no total credits', () => {
      component.userCreditStatus = {
        weeklyCredits: 0,
        purchasedCredits: 0
      };
      
      expect(component.shouldShowPurchasePrompt()).toBe(true);
    });

    it('should show purchase prompt when no purchased credits and insufficient total credits', () => {
      component.userCreditStatus = {
        weeklyCredits: 2,
        purchasedCredits: 0
      };
      component.requiredCredits = 5;
      
      expect(component.shouldShowPurchasePrompt()).toBe(true);
    });

    it('should not show purchase prompt when user has purchased credits', () => {
      component.userCreditStatus = {
        weeklyCredits: 2,
        purchasedCredits: 10
      };
      
      expect(component.shouldShowPurchasePrompt()).toBe(false);
    });
  });

  describe('Insufficient Credits Warning', () => {
    it('should show warning when required credits exceed available credits', () => {
      component.userCreditStatus = {
        weeklyCredits: 2,
        purchasedCredits: 3
      };
      component.requiredCredits = 10;
      component.hasEnoughCredits = false;
      
      expect(component.shouldShowInsufficientCreditsWarning()).toBe(true);
    });

    it('should not show warning when user has enough credits', () => {
      component.userCreditStatus = {
        weeklyCredits: 2,
        purchasedCredits: 10
      };
      component.requiredCredits = 5;
      component.hasEnoughCredits = true;
      
      expect(component.shouldShowInsufficientCreditsWarning()).toBe(false);
    });
  });

  describe('Icon Selection', () => {
    it('should return diamond icon for mixed credits', () => {
      component.userCreditStatus = {
        weeklyCredits: 3,
        purchasedCredits: 10
      };
      
      expect(component.getCreditIcon()).toBe('💎');
    });

    it('should return money icon for purchased credits only', () => {
      component.userCreditStatus = {
        weeklyCredits: 0,
        purchasedCredits: 10
      };
      
      expect(component.getCreditIcon()).toBe('💰');
    });

    it('should return lightning icon for weekly credits only', () => {
      component.userCreditStatus = {
        weeklyCredits: 3,
        purchasedCredits: 0
      };
      
      expect(component.getCreditIcon()).toBe('⚡');
    });

    it('should return diamond icon for no credits', () => {
      component.userCreditStatus = {
        weeklyCredits: 0,
        purchasedCredits: 0
      };
      
      expect(component.getCreditIcon()).toBe('💎');
    });
  });

  describe('Event Emission', () => {
    it('should emit creditActionRequested event on purchase action', () => {
      spyOn(component.creditActionRequested, 'emit');
      
      component.onCreditAction('purchase', 'test-context');
      
      expect(component.creditActionRequested.emit).toHaveBeenCalledWith({
        action: 'purchase',
        context: 'test-context'
      });
    });

    it('should emit creditActionRequested event on view packages action', () => {
      spyOn(component.creditActionRequested, 'emit');
      
      component.onCreditAction('viewPackages');
      
      expect(component.creditActionRequested.emit).toHaveBeenCalledWith({
        action: 'viewPackages',
        context: undefined
      });
    });
  });
});
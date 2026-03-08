import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';

import { OutOfCreditsModalComponent } from './out-of-credits-modal.component';

describe('OutOfCreditsModalComponent', () => {
  let component: OutOfCreditsModalComponent;
  let fixture: ComponentFixture<OutOfCreditsModalComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [OutOfCreditsModalComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(OutOfCreditsModalComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('emits closed when onClose is called', () => {
    spyOn(component.closed, 'emit');

    component.onClose();

    expect(component.closed.emit).toHaveBeenCalled();
  });

  it('emits upgradeClicked when onUpgradeClick is called', () => {
    spyOn(component.upgradeClicked, 'emit');

    component.onUpgradeClick();

    expect(component.upgradeClicked.emit).toHaveBeenCalled();
  });

  it('closes when overlay itself is clicked', () => {
    spyOn(component.closed, 'emit');
    component.isVisible = true;
    fixture.detectChanges();

    const overlay = fixture.nativeElement.querySelector('.modal-overlay') as HTMLElement;
    overlay.click();

    expect(component.closed.emit).toHaveBeenCalled();
  });

  it('does not close when modal card is clicked', () => {
    spyOn(component.closed, 'emit');
    component.isVisible = true;
    fixture.detectChanges();

    const card = fixture.nativeElement.querySelector('.modal-card') as HTMLElement;
    card.click();

    expect(component.closed.emit).not.toHaveBeenCalled();
  });

  it('closes on Escape when visible', () => {
    spyOn(component.closed, 'emit');
    component.isVisible = true;
    fixture.detectChanges();

    const event = new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true });
    document.dispatchEvent(event);

    expect(component.closed.emit).toHaveBeenCalled();
  });

  it('traps focus within modal on Tab', fakeAsync(() => {
    component.isVisible = true;
    fixture.detectChanges();
    tick();

    const closeButton = fixture.nativeElement.querySelector(
      '.modal-close-btn'
    ) as HTMLButtonElement;
    const dismissButton = fixture.nativeElement.querySelector(
      '.modal-dismiss'
    ) as HTMLButtonElement;

    dismissButton.focus();
    const tabEvent = new KeyboardEvent('keydown', { key: 'Tab', bubbles: true, cancelable: true });
    document.dispatchEvent(tabEvent);

    expect(document.activeElement).toBe(closeButton);
  }));
});

import { ChangeDetectorRef } from '@angular/core';
import { of, Subject, throwError } from 'rxjs';
import { PhotoEnhancementComponent } from './photo-enhancement.component';
import { ProfileWorkflowService } from '../../services/profile-workflow.service';
import { ReplicateService } from '../../services/replicate.service';
import { PhotoWorkspaceSessionModule } from './photo-workspace-session';

describe('Photo workspace action recovery', () => {
  let component: PhotoEnhancementComponent;
  let workflow: jasmine.SpyObj<ProfileWorkflowService>;
  let replicate: jasmine.SpyObj<ReplicateService>;

  beforeEach(() => {
    workflow = jasmine.createSpyObj('ProfileWorkflowService', ['createExportPackage']);
    replicate = jasmine.createSpyObj('ReplicateService', ['enhancePhoto']);
    // Exercise the real action methods without starting unrelated account/style subscriptions.
    component = Object.assign(Object.create(PhotoEnhancementComponent.prototype), {
      enhancedImage: { url: '/generated/test.png', storagePath: 'test.png', processedImageId: 1 },
      selectedExportCodes: new Set(['linkedin']),
      arePremiumAugmentationsVisible: true,
      isProcessing: false,
      isApplyingPremiumAugmentation: false,
      isDownloadingPackage: false,
      premiumAugmentations: [{ type: 'relighting', label: 'Relighting' }],
      errorMessage: '',
    });
    Reflect.set(component, '_profileWorkflowService', workflow);
    Reflect.set(component, '_replicateService', replicate);
    Reflect.set(component, '_cdr', { markForCheck: jasmine.createSpy('markForCheck') } as unknown as ChangeDetectorRef);
    spyOn(component, 'downloadEnhanced');
    spyOn(component, 'canApplyPremiumAugmentation').and.callFake(
      () => !component.isApplyingPremiumAugmentation
    );
  });

  it('uses refinement allowance rather than spent candidate slots for the refinement action', () => {
    Object.assign(component, {
      isHeadshotMvpEnabled: true, enhancementType: 'headshot',
      selectedPackageCode: 'pro_package', pendingUpgradePackageCode: null,
      selectedFile: new File(['test'], 'source.jpg'), selectedPortraitStyle: {},
      biometricConsentAccepted: true,
      packageEntitlements: [{ packageCode: 'pro_package', status: 'active',
        remainingPackageUses: 0, remainingCandidates: 0, remainingRefinements: 2 }],
    });
    spyOn(component, 'getCandidateRequestCountForSelectedPackage').and.returnValue(9);
    expect(component.canStartEnhancement()).toBeFalse();
    expect(component.canStartEnhancement(true)).toBeTrue();
    component.packageEntitlements[0].remainingRefinements = 0;
    expect(component.canStartEnhancement(true)).toBeFalse();
  });

  for (const packageCode of ['pro_package', null]) {
    it(`restores paid work without preview promotion (${packageCode ?? 'no remaining allowance'})`, () => {
      Reflect.set(component, 'photoWorkspaceSession', new PhotoWorkspaceSessionModule());
      Reflect.set(component, 'getStorageProxyUrl', (path: string) => path);
      Reflect.set(component, 'selectPortraitStyleByName', () => undefined);
      spyOn(component, 'selectCandidate');
      component.resumePreview({
        processedImageId: 10, imageUrl: '/paid.jpg', storagePath: 'paid.jpg',
        sourceStoragePath: 'source.jpg', style: 'linkedin', createdAt: '',
        hasRawPreview: false, isPaidCandidate: true, canPromotePreview: false,
        activePackageCode: packageCode, remainingCandidateCount: 0,
      });
      expect(component.previewCandidate).toBeNull();
      expect(component.pendingUpgradePackageCode).toBeNull();
      expect(component.selectedPackageCode).toBe(packageCode ?? 'paid_photo');
      expect(component.isPaidGenerationConfirmed()).toBeTrue();
      expect(component.selectCandidate).toHaveBeenCalledWith(jasmine.objectContaining({
        processedImageId: 10, promotedFromPreview: false,
      }));
      expect(replicate.enhancePhoto).not.toHaveBeenCalled();
    });
  }

  it('keeps failed ZIP downloads retryable without silently downloading another format', () => {
    workflow.createExportPackage.and.returnValue(throwError(() => new Error('Network unavailable')));
    component.downloadPackage();
    expect(component.isDownloadingPackage).toBeFalse();
    expect(component.errorMessage).toContain('try again');
    expect(component.downloadEnhanced).not.toHaveBeenCalled();
    component.downloadPackage();
    expect(workflow.createExportPackage).toHaveBeenCalledTimes(2);
  });

  it('ignores a repeated ZIP action while the request is pending', () => {
    workflow.createExportPackage.and.returnValue(new Subject<Blob>());
    component.downloadPackage();
    component.downloadPackage();
    expect(workflow.createExportPackage).toHaveBeenCalledTimes(1);
    expect(component.downloadEnhanced).not.toHaveBeenCalled();
  });

  it('reports a premium application error without throwing from the subscriber', () => {
    replicate.enhancePhoto.and.returnValue(of({ success: false, error: { message: 'Try another photo' } } as any));
    component.applyPremiumAugmentation('relighting');
    expect(component.errorMessage).toBe('Try another photo');
    expect(component.isApplyingPremiumAugmentation).toBeFalse();
    expect(component.enhancedImage?.processedImageId).toBe(1);
  });

  it('does not submit the same premium action twice while processing', () => {
    replicate.enhancePhoto.and.returnValue(new Subject<any>());
    component.applyPremiumAugmentation('relighting');
    component.applyPremiumAugmentation('relighting');
    expect(replicate.enhancePhoto).toHaveBeenCalledTimes(1);
  });
});

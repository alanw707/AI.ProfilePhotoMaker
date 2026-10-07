import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import {
  CareerExportDto,
  CareerPhotoList,
  CareerProfileService,
} from '../../services/career-profile.service';
import { CareerExportPanelComponent } from './career-export-panel.component';

describe('CareerExportPanelComponent', () => {
  let api: jasmine.SpyObj<CareerProfileService>;
  beforeEach(() => {
    api = jasmine.createSpyObj('CareerProfileService', [
      'listPhotos',
      'listExports',
      'createExport',
      'downloadExport',
    ]);
    api.listPhotos.and.returnValue(
      of({ selectedPhotoId: null, selectedPhotoAvailable: false } as unknown as CareerPhotoList)
    );
    api.listExports.and.returnValue(of({ exports: [] }));
    api.createExport.and.returnValue(
      of({ id: 'e1', fileName: 'a.pdf' } as unknown as CareerExportDto)
    );
    api.downloadExport.and.returnValue(of(new Blob(['x'])));
    TestBed.configureTestingModule({
      providers: [{ provide: CareerProfileService, useValue: api }],
    });
  });

  function make() {
    const fixture = TestBed.createComponent(CareerExportPanelComponent);
    fixture.componentRef.setInput('materialId', 'm1');
    fixture.componentRef.setInput('currentVersion', 1);
    fixture.detectChanges();
    return fixture;
  }

  it('keeps the object URL for 60 s, then revokes it once', () => {
    jasmine.clock().install();
    try {
      spyOn(URL, 'createObjectURL').and.returnValue('blob:one');
      const revoke = spyOn(URL, 'revokeObjectURL');
      spyOn(HTMLAnchorElement.prototype, 'click');
      const fixture = make();
      fixture.componentInstance.download();
      jasmine.clock().tick(59_000);
      expect(revoke).not.toHaveBeenCalled();
      jasmine.clock().tick(1_500);
      expect(revoke).toHaveBeenCalledOnceWith('blob:one');
      fixture.destroy();
      expect(revoke).toHaveBeenCalledTimes(1);
    } finally {
      jasmine.clock().uninstall();
    }
  });

  it('revokes remaining object URLs on destroy', () => {
    spyOn(URL, 'createObjectURL').and.returnValues('blob:a', 'blob:b');
    const revoke = spyOn(URL, 'revokeObjectURL');
    spyOn(HTMLAnchorElement.prototype, 'click');
    const fixture = make();
    fixture.componentInstance.download();
    fixture.componentInstance.download();
    expect(revoke).not.toHaveBeenCalled();
    fixture.destroy();
    expect(revoke).toHaveBeenCalledWith('blob:a');
    expect(revoke).toHaveBeenCalledWith('blob:b');
  });
});

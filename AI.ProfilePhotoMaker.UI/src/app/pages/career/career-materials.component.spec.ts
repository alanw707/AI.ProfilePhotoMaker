import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import {
  CareerApiError,
  CareerGoalDto,
  CareerPhotoList,
  CareerRunDto,
  CareerProfileService,
} from '../../services/career-profile.service';
import { CareerMaterialsComponent } from './career-materials.component';

describe('CareerMaterialsComponent', () => {
  const goalId = '3f2b8c1e-6a4d-4e0b-9d57-1c2a3b4c5d6e';
  const list: CareerPhotoList = {
    photos: [],
    selectedPhotoId: null,
    selectedPhotoAvailable: true,
    entitlements: [],
  };
  let api: jasmine.SpyObj<CareerProfileService>;
  let router: Router;

  function create(careerGoal: string | null = null) {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: CareerProfileService, useValue: api },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParamMap: { get: () => careerGoal } } },
        },
      ],
    });
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    spyOn(router, 'navigateByUrl').and.resolveTo(true);
    const fixture = TestBed.createComponent(CareerMaterialsComponent);
    fixture.detectChanges();
    return fixture.componentInstance;
  }

  beforeEach(() => {
    api = jasmine.createSpyObj('CareerProfileService', [
      'listPhotos',
      'getGoal',
      'selectPhoto',
      'clearPhoto',
      'listMaterials',
      'createRun',
    ]);
    api.listMaterials.and.returnValue(of({ materials: [] }));
    api.getGoal.and.returnValue(
      throwError(() => ({ kind: 'notFound', message: '' }) as CareerApiError)
    );
  });

  it('sends an expired session to sign-in with a return to materials', () => {
    api.listPhotos.and.returnValue(
      throwError(() => ({ kind: 'unauthorized', message: '' }) as CareerApiError)
    );
    create();
    expect(router.navigate).toHaveBeenCalledWith(['/auth/login'], {
      queryParams: { returnUrl: '/app/career/materials' },
    });
  });

  it('sends a disabled workspace to the app home', () => {
    api.listPhotos.and.returnValue(
      throwError(() => ({ kind: 'disabled', message: '' }) as CareerApiError)
    );
    create();
    expect(router.navigateByUrl).toHaveBeenCalledWith('/app');
  });

  it('keeps the saved photo checked and marked in use', () => {
    const photo = {
      id: 42,
      imageUrl: 'https://img/42.png',
      createdAt: '2026-09-28T10:00:00Z',
      style: 'linkedin',
      isWatermarkedPreview: false,
    };
    const other = { ...photo, id: 43 };
    api.listPhotos.and.returnValue(of({ ...list, photos: [photo, other], selectedPhotoId: 42 }));
    api.selectPhoto.and.returnValue(
      of({ selectedPhotoId: 43, careerGoalId: null, selectedAt: '2026-10-04T00:00:00Z' })
    );
    const component = create();
    expect(component.pendingPhotoId()).toBe(42);
    expect(component.isInUse(photo)).toBeTrue();
    expect(component.canSave()).toBeFalse();

    component.choose(other);
    expect(component.canSave()).toBeTrue();
    component.useSelected();
    expect(component.pendingPhotoId()).toBe(43);
    expect(component.isInUse(other)).toBeTrue();
    expect(component.isInUse(photo)).toBeFalse();
    expect(component.canSave()).toBeFalse();
  });

  it('flags a changed goal only when the arriving goal differs', () => {
    api.listPhotos.and.returnValue(of(list));
    api.getGoal.and.returnValue(of({ id: goalId } as CareerGoalDto));
    expect(create('9a8b7c6d-5e4f-4a3b-8c2d-1e0f9a8b7c6d').goalChanged()).toBeTrue();
    TestBed.resetTestingModule();
    expect(create(goalId).goalChanged()).toBeFalse();
    TestBed.resetTestingModule();
    expect(create(null).goalChanged()).toBeFalse();
  });

  it('no longer shows the placeholder and lists both kinds in words', () => {
    api.listPhotos.and.returnValue(of(list));
    api.listMaterials.and.returnValue(
      of({
        materials: [
          {
            id: 'r1',
            kind: 'resume',
            title: 'My resume',
            stale: true,
            currentVersion: 2,
            updatedAt: '2026-10-05T10:00:00Z',
          },
          {
            id: 's1',
            kind: 'summary',
            title: 'My summary',
            stale: false,
            currentVersion: 1,
            updatedAt: '2026-10-04T10:00:00Z',
          },
        ],
      })
    );
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: CareerProfileService, useValue: api },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: { get: () => null } } } },
      ],
    });
    const fixture = TestBed.createComponent(CareerMaterialsComponent);
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).not.toContain('arrive in a later release');
    expect(text).toContain('Professional summary');
    expect(text).toContain('Resume');
    expect(text).toContain('Out of date');
    expect(text).toContain('Oct');
    expect(text).not.toContain('2026-10-05');
  });

  it('starts a professional summary run and opens the editor', () => {
    api.listPhotos.and.returnValue(of(list));
    api.createRun.and.returnValue(of({ id: 'run-9' } as unknown as CareerRunDto));
    const component = create();
    component.draftSummary();
    expect(api.createRun.calls.mostRecent().args[1]).toBe('professional_summary');
    expect(router.navigate).toHaveBeenCalledWith(['/app/career/summary-draft'], {
      queryParams: { run: 'run-9' },
    });
  });
});

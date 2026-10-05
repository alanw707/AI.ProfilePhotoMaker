import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import {
  CareerApiError,
  CareerGoalDto,
  CareerPhotoList,
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
    ]);
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

  it('flags a changed goal only when the arriving goal differs', () => {
    api.listPhotos.and.returnValue(of(list));
    api.getGoal.and.returnValue(of({ id: goalId } as CareerGoalDto));
    expect(create('9a8b7c6d-5e4f-4a3b-8c2d-1e0f9a8b7c6d').goalChanged()).toBeTrue();
    TestBed.resetTestingModule();
    expect(create(goalId).goalChanged()).toBeFalse();
    TestBed.resetTestingModule();
    expect(create(null).goalChanged()).toBeFalse();
  });
});

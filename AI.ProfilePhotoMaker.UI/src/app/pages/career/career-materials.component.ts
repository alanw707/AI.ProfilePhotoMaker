import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  CareerApiError,
  CareerPhotoDto,
  CareerPhotoEntitlement,
  CareerProfileService,
  ResumeMaterialSummary,
  PLAIN_503,
} from '../../services/career-profile.service';
import { careerHandoffQuery } from './career-return';
import { clearStartKey, releaseStartKey, startKey } from './career-run';
import { dateText } from './market-format';

const MATERIALS_PATH = '/app/career/materials';
const SUMMARY_KEY = 'career-materials-summary-start-key';

@Component({
  standalone: true,
  selector: 'app-career-materials',
  imports: [RouterLink, DatePipe],
  templateUrl: './career-materials.component.html',
  styleUrl: './career.scss',
})
export class CareerMaterialsComponent implements OnInit {
  private api = inject(CareerProfileService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  loading = signal(true);
  photos = signal<CareerPhotoDto[]>([]);
  entitlements = signal<CareerPhotoEntitlement[]>([]);
  selectedPhotoId = signal<number | null>(null);
  selectedPhotoAvailable = signal(true);
  /** The photo the user is currently choosing in the grid (not yet saved). */
  pendingPhotoId = signal<number | null>(null);
  goalId = signal<string | null>(null);
  goalChanged = signal(false);
  saving = signal(false);
  materials = signal<ResumeMaterialSummary[]>([]);
  materialsLoading = signal(true);
  materialsError = signal('');
  drafting = signal(false);
  status = signal('');
  error = signal('');

  selectedPhoto = computed(() => {
    const id = this.selectedPhotoId();
    return this.photos().find(photo => photo.id === id) ?? null;
  });
  /** True when the saved choice points at a photo that can no longer be used. */
  selectedUnavailable = computed(
    () => this.selectedPhotoId() !== null && !this.selectedPhotoAvailable()
  );
  hasSelection = computed(() => this.selectedPhotoId() !== null && this.selectedPhotoAvailable());
  /** Saving is only offered for a new choice, not for the photo already in use. */
  canSave = computed(
    () =>
      this.pendingPhotoId() !== null &&
      !this.saving() &&
      !(this.hasSelection() && this.pendingPhotoId() === this.selectedPhotoId())
  );

  ngOnInit() {
    this.api.listMaterials().subscribe({
      next: list => {
        this.materials.set(list.materials ?? []);
        this.materialsLoading.set(false);
      },
      error: (e: CareerApiError) => {
        this.materialsLoading.set(false);
        if (e.kind === 'unauthorized' || e.kind === 'disabled') {
          this.handle(e);
        } else {
          this.materialsError.set('We could not load your drafts. Try again later.');
        }
      },
    });
    this.api.listPhotos().subscribe({
      next: list => {
        this.photos.set(list.photos);
        this.entitlements.set(list.entitlements);
        this.selectedPhotoId.set(list.selectedPhotoId);
        this.selectedPhotoAvailable.set(list.selectedPhotoAvailable);
        // Start the grid on the photo already in use, so it reads as checked.
        this.pendingPhotoId.set(list.selectedPhotoAvailable ? list.selectedPhotoId : null);
        this.loading.set(false);
      },
      error: (e: CareerApiError) => {
        this.loading.set(false);
        this.handle(e);
      },
    });
    this.api.getGoal().subscribe({
      next: goal => {
        this.goalId.set(goal.id);
        const arrivedWith = this.route.snapshot.queryParamMap.get('careerGoal');
        this.goalChanged.set(arrivedWith !== null && arrivedWith !== goal.id);
      },
      error: (e: CareerApiError) => {
        if (e.kind !== 'notFound') {
          this.handle(e);
        }
      },
    });
  }

  kindText(m: ResumeMaterialSummary): string {
    return m.kind === 'summary' ? 'Professional summary' : 'Resume';
  }
  updatedText(m: ResumeMaterialSummary): string {
    const text = dateText(String(m.updatedAt ?? '').slice(0, 10));
    return /^\d{4}-\d{2}-\d{2}$/.test(text) ? 'Updated recently' : `Updated ${text}`;
  }
  openPath(m: ResumeMaterialSummary): string {
    return m.kind === 'summary' ? '/app/career/summary-draft' : '/app/career/resume';
  }

  draftSummary() {
    if (this.drafting()) {
      return;
    }
    this.drafting.set(true);
    this.materialsError.set('');
    this.api.createRun(startKey(SUMMARY_KEY), 'professional_summary').subscribe({
      next: run => {
        this.drafting.set(false);
        clearStartKey(SUMMARY_KEY);
        this.router.navigate(['/app/career/summary-draft'], { queryParams: { run: run.id } });
      },
      error: (e: CareerApiError) => {
        this.drafting.set(false);
        releaseStartKey(SUMMARY_KEY, e);
        if (e.kind === 'unauthorized' || e.kind === 'disabled') {
          this.handle(e);
        } else if (PLAIN_503[e.kind]) {
          this.materialsError.set(PLAIN_503[e.kind] as string);
        } else if (e.kind === 'allowance') {
          this.materialsError.set(
            'You have used this period’s drafting allowance, so we cannot draft a new summary now. You can still open, edit and download what you have.'
          );
        } else if (e.kind === 'profileRequired') {
          this.materialsError.set('Add your profile first so we have facts to use.');
        } else {
          this.materialsError.set('We could not start your summary. Try again.');
        }
      },
    });
  }

  workspaceQuery(photo?: CareerPhotoDto) {
    return careerHandoffQuery(this.goalId(), photo?.id);
  }

  entitlementLine(entitlement: CareerPhotoEntitlement): string {
    const candidates = this.count(entitlement.remainingCandidates, 'candidate');
    const refinements = this.count(entitlement.remainingRefinements, 'refinement');
    return `${entitlement.packageName}: ${candidates}, ${refinements} left`;
  }

  photoLabel(photo: CareerPhotoDto): string {
    const date = new Date(photo.createdAt).toLocaleDateString('en-US', { dateStyle: 'medium' });
    return photo.style ? `Your photo from ${date}, ${photo.style}` : `Your photo from ${date}`;
  }

  isInUse(photo: CareerPhotoDto): boolean {
    return this.hasSelection() && this.selectedPhotoId() === photo.id;
  }

  choose(photo: CareerPhotoDto) {
    if (!photo.isWatermarkedPreview) {
      this.pendingPhotoId.set(photo.id);
    }
  }

  useSelected() {
    const id = this.pendingPhotoId();
    if (id === null || !this.canSave()) {
      return;
    }
    this.saving.set(true);
    this.status.set('');
    this.error.set('');
    this.api.selectPhoto(id).subscribe({
      next: result => {
        this.saving.set(false);
        this.selectedPhotoId.set(result.selectedPhotoId);
        this.selectedPhotoAvailable.set(true);
        this.pendingPhotoId.set(result.selectedPhotoId);
        this.status.set('Photo saved to your career profile.');
      },
      error: (e: CareerApiError) => {
        this.saving.set(false);
        this.handle(e);
      },
    });
  }

  stopUsing() {
    this.status.set('');
    this.error.set('');
    this.api.clearPhoto().subscribe({
      next: () => {
        this.selectedPhotoId.set(null);
        this.selectedPhotoAvailable.set(true);
        this.pendingPhotoId.set(null);
        this.status.set('Photo removed from your career profile.');
      },
      error: (e: CareerApiError) => this.handle(e),
    });
  }

  private count(value: number, noun: string): string {
    return `${value} ${noun}${value === 1 ? '' : 's'}`;
  }

  private handle(e: CareerApiError) {
    if (e.kind === 'unauthorized') {
      this.router.navigate(['/auth/login'], { queryParams: { returnUrl: MATERIALS_PATH } });
    } else if (e.kind === 'disabled') {
      this.router.navigateByUrl('/app');
    } else if (e.kind === 'preview') {
      this.error.set('Watermarked preview - improve it in the photo workspace first.');
    } else if (e.kind === 'notFound') {
      this.error.set('That photo is no longer available. Choose another one.');
      this.pendingPhotoId.set(null);
    } else {
      this.error.set(e.message);
    }
  }
}

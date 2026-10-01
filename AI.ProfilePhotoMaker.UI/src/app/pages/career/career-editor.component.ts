import { Component, ElementRef, OnInit, ViewChild, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { CareerApiError, CareerGoalDto, CareerProfileDto, CareerProfileService, ProfileVersion, ProfileVersionDetail } from '../../services/career-profile.service';

function payRange(control: AbstractControl): ValidationErrors | null {
  const { desiredPayMin: min, desiredPayMax: max } = control.value;
  return min !== null && max !== null && max < min ? { payRange: true } : null;
}
@Component({ standalone: true, selector: 'app-career-editor', imports: [ReactiveFormsModule, RouterLink, DatePipe], templateUrl: './career-editor.component.html', styleUrl: './career.scss' })
export class CareerEditorComponent implements OnInit {
  private fb = inject(FormBuilder);
  private api = inject(CareerProfileService);
  private router = inject(Router);
  @ViewChild('errorSummary') errorSummary?: ElementRef<HTMLElement>;
  setup = this.router.url.includes('/setup');
  step = signal(1);
  profile = signal<CareerProfileDto | null>(null);
  goal = signal<CareerGoalDto | null>(null);
  versions = signal<ProfileVersion[]>([]);
  selectedVersion = signal<ProfileVersionDetail | null>(null);
  error = signal('');
  conflict = signal(false);
  status = signal('');
  fieldErrors = signal<Record<string, string>>({});
  saving = signal(false);
  errorForm = signal<'profile' | 'goal'>('profile');
  skillInput = '';
  highlightInput = '';
  profileForm = this.fb.group({
    currentTitle: ['', [Validators.required, Validators.maxLength(120)]],
    industry: ['', Validators.maxLength(120)], yearsExperience: [null as number | null, [Validators.min(0), Validators.max(60)]],
    location: ['', Validators.maxLength(120)], summary: ['', Validators.maxLength(2000)],
    workArrangement: [''], skills: [[] as string[]], highlights: [[] as string[]], confirmed: [false, Validators.requiredTrue],
  });
  goalForm = this.fb.group({
    targetRole: ['', [Validators.required, Validators.maxLength(120)]], targetLocation: ['', Validators.maxLength(120)],
    workArrangement: [''], desiredPayMin: [null as number | null, [Validators.min(0), Validators.max(1000000)]],
    desiredPayMax: [null as number | null, [Validators.min(0), Validators.max(1000000)]],
    weeklyEffortHours: [null as number | null, [Validators.min(1), Validators.max(40)]], confirmed: [false, Validators.requiredTrue],
  }, { validators: payRange });
  ngOnInit() { this.load(); }
  load() {
    this.api.getProfile().subscribe({ next: p => {
      this.profile.set(p);
      this.profileForm.patchValue({ ...p.facts, confirmed: false });
      this.loadVersions();
    }, error: (e: CareerApiError) => this.handle(e, true) });
    this.api.getGoal().subscribe({ next: g => { this.goal.set(g); this.goalForm.patchValue({ ...g.goal, confirmed: false }); }, error: (e: CareerApiError) => this.handle(e, true) });
  }
  reloadLatest() { this.conflict.set(false); this.error.set(''); this.load(); }
  private loadVersions() { this.api.profileVersions().subscribe({ next: v => this.versions.set(v), error: e => this.handle(e) }); }
  viewVersion(n: number) { this.api.profileVersion(n).subscribe({ next: v => this.selectedVersion.set(v), error: e => this.handle(e) }); }
  restoreVersion(n: number) {
    this.api.restoreProfile(n).subscribe({ next: p => { this.profile.set(p); this.selectedVersion.set(null); this.profileForm.patchValue({ ...p.facts, confirmed: false }); this.loadVersions(); this.status.set('Profile version restored.'); }, error: e => this.handle(e) });
  }
  addItem(field: 'skills' | 'highlights', input: HTMLInputElement) {
    const value = input.value.trim(); const items = this.profileForm.controls[field].value ?? [];
    const limit = field === 'skills' ? 50 : 20;
    const length = field === 'skills' ? 80 : 300;
    if (value && value.length <= length && items.length < limit && (field !== 'skills' || !items.some(i => i.toLowerCase() === value.toLowerCase()))) {
      this.profileForm.controls[field].setValue([...items, value]); input.value = '';
    }
  }
  removeItem(field: 'skills' | 'highlights', index: number) {
    this.profileForm.controls[field].setValue((this.profileForm.controls[field].value ?? []).filter((_, i) => i !== index));
  }
  hasError(name: string, goal = false): boolean {
    const form = goal ? this.goalForm : this.profileForm;
    const control: AbstractControl | null = goal ? this.goalForm.get(name) : this.profileForm.get(name);
    return !!this.fieldErrors()[name] || !!(control?.invalid && control.touched) || (name === 'desiredPayMax' && !!form.errors?.['payRange']);
  }
  message(name: string, goal = false): string {
    if (this.fieldErrors()[name]) {return this.fieldErrors()[name];}
    if (name === 'desiredPayMax' && this.goalForm.errors?.['payRange']) {return 'Maximum pay must be at least minimum pay.';}
    const errors = (goal ? this.goalForm.get(name) : this.profileForm.get(name))?.errors;
    if (errors?.['required'] || errors?.['requiredTrue']) {return 'Required.';}
    if (errors?.['min'] || errors?.['max']) {return 'Outside the allowed range.';}
    return 'Too long.';
  }
  submitProfile() {
    this.profileForm.markAllAsTouched();
    if (this.profileForm.invalid) {return this.failValidation('profile');}
    this.saving.set(true); this.error.set(''); this.fieldErrors.set({});
    this.errorForm.set('profile');
    this.api.saveProfile(this.profileForm.getRawValue() as Parameters<CareerProfileService['saveProfile']>[0]).subscribe({
      next: p => { this.saving.set(false); this.profile.set(p); this.status.set('Professional facts saved.'); this.loadVersions(); if (this.setup) {this.step.set(2);} },
      error: e => { this.saving.set(false); this.handle(e); },
    });
  }
  submitGoal() {
    this.goalForm.markAllAsTouched();
    if (this.goalForm.invalid) {return this.failValidation('goal');}
    this.saving.set(true); this.error.set(''); this.fieldErrors.set({});
    const goal = this.goalForm.getRawValue() as Parameters<CareerProfileService['createGoal']>[0];
    const request = this.goal() ? this.api.updateGoal(this.goal()!.id, goal) : this.api.createGoal(goal);
    this.errorForm.set('goal');
    request.subscribe({ next: g => { this.saving.set(false); this.goal.set(g); this.status.set('Goal saved.'); if (this.setup) {this.router.navigateByUrl('/app/career');} }, error: e => { this.saving.set(false); this.handle(e); } });
  }
  private failValidation(form: 'profile' | 'goal') {
    this.errorForm.set(form);
    this.error.set('Please correct the following fields before saving.');
    this.focusSummary();
  }
  private focusSummary() { setTimeout(() => this.errorSummary?.nativeElement.focus()); }
  private handle(e: CareerApiError, quietNotFound = false) {
    if (e.kind === 'disabled') { this.router.navigateByUrl('/app'); return; }
    if (e.kind === 'notFound' && quietNotFound) {return;}
    if (e.kind === 'conflict' || e.kind === 'precondition') { this.conflict.set(true); this.error.set('This was changed in another tab or device. Reload the latest version.'); return; }
    this.fieldErrors.set(e.fieldErrors ?? {});
    for (const key of Object.keys(e.fieldErrors ?? {})) { this.profileForm.get(key)?.markAsTouched(); this.goalForm.get(key)?.markAsTouched(); }
    this.error.set(e.kind === 'validation' ? 'Please correct the following fields before saving.' : e.message);
    this.focusSummary();
  }
}

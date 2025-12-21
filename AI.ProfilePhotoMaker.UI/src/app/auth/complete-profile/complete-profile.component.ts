import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { AuthService, ProfileCompletionDto } from '../../services/auth.service';
import { ThemeService } from '../../services/theme.service';

@Component({
  selector: 'app-complete-profile',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  templateUrl: './complete-profile.component.html',
  styleUrls: ['./complete-profile.component.sass'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CompleteProfileComponent implements OnInit {
  profileForm: FormGroup;
  loading = false;
  error = '';

  private readonly _formBuilder = inject(FormBuilder);
  private readonly _authService = inject(AuthService);
  private readonly _router = inject(Router);
  public readonly themeService = inject(ThemeService);

  constructor() {
    this.profileForm = this._formBuilder.group({
      firstName: ['', [Validators.required, Validators.minLength(2)]],
      lastName: ['', [Validators.required, Validators.minLength(2)]],
      gender: ['', Validators.required],
      ethnicity: ['', Validators.required],
      ageConfirmed: [false, Validators.requiredTrue],
    });
  }

  ngOnInit(): void {
    // Pre-fill form with existing user data if available
    this._authService.currentUser$.subscribe(user => {
      if (user && (user.firstName || user.lastName)) {
        this.profileForm.patchValue({
          firstName: user.firstName,
          lastName: user.lastName,
        });
      }
    });
  }

  get f(): Record<string, AbstractControl> {
    return this.profileForm.controls;
  }

  shouldShowFirstNameError(): boolean {
    return this.f['firstName'].invalid && this.f['firstName'].touched;
  }

  shouldShowLastNameError(): boolean {
    return this.f['lastName'].invalid && this.f['lastName'].touched;
  }

  shouldShowGenderError(): boolean {
    return this.f['gender'].invalid && this.f['gender'].touched;
  }

  shouldShowEthnicityError(): boolean {
    return this.f['ethnicity'].invalid && this.f['ethnicity'].touched;
  }

  shouldShowAgeError(): boolean {
    return this.f['ageConfirmed'].invalid && this.f['ageConfirmed'].touched;
  }

  onSubmit(): void {
    this.error = '';

    if (this.profileForm.invalid) {
      // Mark all fields as touched to show validation errors
      Object.keys(this.profileForm.controls).forEach(key => {
        this.profileForm.get(key)?.markAsTouched();
      });
      return;
    }

    this.loading = true;
    const profileData: ProfileCompletionDto = {
      firstName: this.f['firstName'].value,
      lastName: this.f['lastName'].value,
      gender: this.f['gender'].value,
      ethnicity: this.f['ethnicity'].value,
      ageConfirmed: this.f['ageConfirmed'].value,
    };

    this._authService.completeProfile(profileData).subscribe({
      next: response => {
        this.loading = false;
        if (response.success) {
          // Profile completed successfully, navigate to dashboard
          this._router.navigate(['/app/dashboard']);
        } else {
          this.error = response.message || 'Failed to complete profile';
        }
      },
      error: error => {
        this.loading = false;
        this.error = error?.error?.error || error?.message || 'Profile completion failed. Please try again.';
      },
    });
  }

  toggleTheme(): void {
    this.themeService.toggleTheme();
  }
}

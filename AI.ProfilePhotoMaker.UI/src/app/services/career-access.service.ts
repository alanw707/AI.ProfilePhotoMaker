import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AuthService } from './auth.service';
import { ConfigService } from './config.service';

/**
 * Whether the signed-in account may use the career workspace. When the public config opens career
 * to everyone no request is made; during an allowlist rollout the server answers per account.
 * The career API enforces the same rule, so a stale answer can only hide or show navigation.
 */
@Injectable({ providedIn: 'root' })
export class CareerAccessService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(ConfigService);
  private readonly auth = inject(AuthService);
  private pending: Promise<boolean> | null = null;

  /** Last resolved answer; false until resolved. */
  readonly granted = signal(false);

  constructor() {
    // A different (or no) account needs a fresh answer.
    this.auth.isAuthenticated$.subscribe(() => {
      this.pending = null;
      this.granted.set(false);
    });
  }

  resolve(): Promise<boolean> {
    if (this.config.isCareerWorkspaceEnabled) {
      this.granted.set(true);
      return Promise.resolve(true);
    }
    if (!this.auth.isAuthenticated()) {
      this.granted.set(false);
      return Promise.resolve(false);
    }
    this.pending ??= firstValueFrom(
      this.http.get<{ data?: { enabled?: boolean } }>(`${this.config.baseUrl}/config/career-access`)
    )
      .then(
        response => response?.data?.enabled === true,
        () => false
      )
      .then(enabled => {
        this.granted.set(enabled);
        return enabled;
      });
    return this.pending;
  }
}

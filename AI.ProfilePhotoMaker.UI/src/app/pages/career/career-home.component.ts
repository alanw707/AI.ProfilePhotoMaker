import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import {
  CareerGoalDto,
  CareerProfileDto,
  CareerProfileService,
  CareerApiError,
} from '../../services/career-profile.service';

@Component({
  standalone: true,
  selector: 'app-career-home',
  imports: [RouterLink, DatePipe],
  template: ` <main class="career-page">
    <div class="career-sheet">
      <h1>Career workspace</h1>
      <p>
        Keep your professional facts and next goal in one place. You enter and confirm every detail.
      </p>
      @if (profile(); as p) {
        <section aria-labelledby="profile-heading">
          <h2 id="profile-heading">Professional profile</h2>
          <p>
            {{ p.facts.currentTitle }}
            @if (p.facts.industry) {
              · {{ p.facts.industry }}
            }
          </p>
          <p class="muted">
            {{ sourceLabel(p.provenance.source) }} · confirmed
            {{ p.provenance.confirmedAt | date: 'mediumDate' }}
          </p>
          <a routerLink="/app/career/profile">View and edit profile</a>
        </section>
      } @else if (!loading()) {
        <section>
          <h2>Start with your facts</h2>
          <p>No professional profile saved yet.</p>
          <a class="primary" routerLink="/app/career/setup">Set up your profile and goal</a>
        </section>
      }
      @if (goal(); as g) {
        <section aria-labelledby="goal-heading">
          <h2 id="goal-heading">Your goal</h2>
          <p>{{ g.goal.targetRole }}</p>
          @if (g.occupation; as o) {
            <p data-occupation>Occupation: {{ o.title }} ({{ o.code }})</p>
          }
          @if (g.isStale) {
            <p class="caution">
              Needs review · Your profile changed since you confirmed this goal.
            </p>
          }
          <a routerLink="/app/career/profile">{{
            g.isStale ? 'Review your goal' : 'Edit your goal'
          }}</a>
        </section>
      }
      <p>
        <a routerLink="/app/career/import">Import from a resume</a>
      </p>
      <p>
        <a routerLink="/app/career/summary">Draft a profile summary</a>
      </p>
      <p>
        <a routerLink="/app/career/occupation">Confirm your occupation</a>
      </p>
      <p>
        <a routerLink="/app/career/market">Analytics: market brief</a>
      </p>
      <p>
        <a routerLink="/app/career/markets">Compare markets</a>
      </p>
      <p>
        <a routerLink="/app/career/pay">Pay analysis</a>
      </p>
      <p>
        <a routerLink="/app/career/roadmap">Career roadmap</a>
      </p>
      <p>
        <a routerLink="/app/career/resume">Targeted resume</a>
      </p>
      <p>
        <a routerLink="/app/career/summary-draft">Professional summary</a>
      </p>
      <p>
        <a routerLink="/app/career/jobs">Open postings</a>
      </p>
      <p>
        <a routerLink="/app/career/materials">Materials and photo</a>
      </p>
      @if (error()) {
        <p role="alert">{{ error() }}</p>
      }
    </div>
  </main>`,
  styleUrl: './career.scss',
})
export class CareerHomeComponent implements OnInit {
  private api = inject(CareerProfileService);
  private router = inject(Router);
  profile = signal<CareerProfileDto | null>(null);
  goal = signal<CareerGoalDto | null>(null);
  loading = signal(true);
  error = signal('');
  ngOnInit() {
    this.api.getProfile().subscribe({
      next: p => {
        this.profile.set(p);
        this.loading.set(false);
      },
      error: (e: CareerApiError) => {
        this.loading.set(false);
        this.handle(e);
      },
    });
    this.api.getGoal().subscribe({ next: g => this.goal.set(g), error: e => this.handle(e) });
  }
  sourceLabel(source: string): string {
    if (source === 'resume') {
      return 'From your resume';
    }
    return source === 'pasted' ? 'From pasted text' : 'Entered manually';
  }
  private handle(e: CareerApiError) {
    if (e.kind === 'disabled') {
      this.router.navigateByUrl('/app');
    } else if (e.kind !== 'notFound') {
      this.error.set(e.message);
    }
  }
}

import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { MarketingService } from '../../services/marketing.service';

type State = 'loading' | 'success' | 'error' | 'invalid';

@Component({
  selector: 'app-unsubscribe',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './unsubscribe.component.html',
  styleUrls: ['./unsubscribe.component.sass'],
})
export class UnsubscribeComponent implements OnInit {
  state: State = 'loading';

  constructor(
    private _route: ActivatedRoute,
    private _marketing: MarketingService
  ) {}

  ngOnInit(): void {
    const token = this._route.snapshot.queryParamMap.get('token');
    if (!token) {
      this.state = 'invalid';
      return;
    }
    this._marketing.unsubscribe(token).subscribe({
      next: () => {
        this.state = 'success';
      },
      error: () => {
        this.state = 'error';
      },
    });
  }
}

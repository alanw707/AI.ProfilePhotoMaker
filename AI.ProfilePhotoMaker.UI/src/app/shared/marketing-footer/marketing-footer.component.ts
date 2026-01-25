import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { CookieConsentService } from '../../services/cookie-consent.service';

@Component({
  selector: 'app-marketing-footer',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './marketing-footer.component.html',
  styleUrls: ['./marketing-footer.component.sass'],
})
export class MarketingFooterComponent {
  readonly currentYear = new Date().getFullYear();

  constructor(private readonly cookieConsentService: CookieConsentService) {}

  openCookiePreferences(): void {
    this.cookieConsentService.requestPreferencesOpen();
  }
}

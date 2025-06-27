import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { ThemeService } from '../../services/theme.service';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-header-navigation',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './header-navigation.component.html',
  styleUrls: ['./header-navigation.component.sass']
})
export class HeaderNavigationComponent implements OnInit, OnDestroy {
  userName: string = '';
  userEmail: string = '';
  private userSubscription?: Subscription;

  constructor(
    private authService: AuthService,
    private router: Router,
    public themeService: ThemeService
  ) {}

  ngOnInit() {
    this.userSubscription = this.authService.currentUser$.subscribe(user => {
      if (user) {
        this.userEmail = user.email;
        this.userName = `${user.firstName || ''} ${user.lastName || ''}`.trim() || this.userEmail.split('@')[0];
      }
    });
  }

  ngOnDestroy() {
    if (this.userSubscription) {
      this.userSubscription.unsubscribe();
    }
  }

  toggleTheme() {
    this.themeService.toggleTheme();
  }

  logout() {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}
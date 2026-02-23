import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

export type Theme = 'light' | 'dark';

@Injectable({
  providedIn: 'root',
})
export class ThemeService {
  private _currentTheme = new BehaviorSubject<Theme>('light');
  public theme$ = this._currentTheme.asObservable();

  constructor() {
    // Check for saved theme preference or default to light
    const savedTheme = localStorage.getItem('theme') as Theme;

    const initialTheme = savedTheme || 'light';
    this.setTheme(initialTheme);
  }

  setTheme(theme: Theme): void {
    this._currentTheme.next(theme);
    localStorage.setItem('theme', theme);
    // Ensure theme attribute is properly set and force a style recalculation
    document.documentElement.setAttribute('data-theme', theme);
    // Trigger a style recalculation to ensure CSS variables are applied
    document.documentElement.style.display = 'none';
    void document.documentElement.offsetHeight; // Force reflow
    document.documentElement.style.display = '';
  }

  toggleTheme(): void {
    const currentTheme = this._currentTheme.value;
    const newTheme = currentTheme === 'light' ? 'dark' : 'light';
    this.setTheme(newTheme);
  }

  getCurrentTheme(): Theme {
    return this._currentTheme.value;
  }

  isDark(): boolean {
    return this._currentTheme.value === 'dark';
  }
}

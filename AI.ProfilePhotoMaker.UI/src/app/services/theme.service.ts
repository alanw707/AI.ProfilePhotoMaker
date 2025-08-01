import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

export type Theme = 'light' | 'dark';

@Injectable({
  providedIn: 'root',
})
export class ThemeService {
  private _currentTheme = new BehaviorSubject<Theme>('dark');
  public theme$ = this._currentTheme.asObservable();

  constructor() {
    // Check for saved theme preference or default to dark
    const savedTheme = localStorage.getItem('theme') as Theme;

    const initialTheme = savedTheme || 'dark';
    this.setTheme(initialTheme);
  }

  setTheme(theme: Theme): void {
    this._currentTheme.next(theme);
    localStorage.setItem('theme', theme);
    document.documentElement.setAttribute('data-theme', theme);
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

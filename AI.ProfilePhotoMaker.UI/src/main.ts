import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { AppComponent } from './app/app.component';
import { isDevMode } from '@angular/core';
import { initToolbar } from '@21st-extension/toolbar';

if (isDevMode()) {
  initToolbar({
    plugins: [],
  });
}

bootstrapApplication(AppComponent, appConfig)
  .catch((err) => console.error(err));

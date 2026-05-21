import { APP_INITIALIZER, ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideRouter, withInMemoryScrolling } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';

import { routes } from './app.routes';
import { simpleAuthInterceptor } from './interceptors/simple-auth.interceptor';
import { ConfigService } from './services/config.service';

function initializeClientConfig(configService: ConfigService): () => Promise<void> {
  return () => configService.loadClientConfiguration();
}

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(
      routes,
      withInMemoryScrolling({
        scrollPositionRestoration: 'enabled',
        anchorScrolling: 'enabled',
      })
    ),
    provideHttpClient(withInterceptors([simpleAuthInterceptor])),
    {
      provide: APP_INITIALIZER,
      useFactory: initializeClientConfig,
      deps: [ConfigService],
      multi: true,
    },
  ],
};

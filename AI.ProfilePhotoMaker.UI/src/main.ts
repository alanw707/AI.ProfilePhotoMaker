import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { AppComponent } from './app/app.component';
import { environment } from './environments/environment';
import { AuthService } from './app/services/auth.service';
import { FileUploadService } from './app/services/file-upload.service';
import { ReplicateService } from './app/services/replicate.service';
import { ImageStateService } from './app/services/image-state.service';

bootstrapApplication(AppComponent, appConfig)
  .then(appRef => {
    if (typeof window !== 'undefined') {
      const debugContext: any = {
        environment,
        injector: appRef.injector,
        services: {},
      };

      const tryGet = <T>(token: any): T | null => {
        try {
          return appRef.injector.get(token);
        } catch {
          return null;
        }
      };

      const authService = tryGet<AuthService>(AuthService);
      if (authService) {
        debugContext.services.authService = authService;
        (window as any).authService = authService;
      }

      const fileUploadService = tryGet<FileUploadService>(FileUploadService);
      if (fileUploadService) {
        debugContext.services.fileUploadService = fileUploadService;
        (window as any).fileUploadService = fileUploadService;
      }

      const replicateService = tryGet<ReplicateService>(ReplicateService);
      const imageStateService = tryGet<ImageStateService>(ImageStateService);
      if (replicateService) {
        debugContext.services.replicateService = replicateService;
      }

      if (imageStateService) {
        debugContext.services.imageStateService = imageStateService;
      }

      (window as any).__APP_DEBUG__ = debugContext;
      (window as any).environment = environment;
    }
  })
  .catch(err => console.error(err));

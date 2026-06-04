import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { environment } from '../../environments/environment';
import { StylePreviewService } from './style-preview.service';

const originalBackendUrl = (environment.azure as { backendUrl?: string } | undefined)?.backendUrl;
const originalBaseUrl = environment.baseUrl;

describe('StylePreviewService', () => {
  let service: StylePreviewService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    if (!environment.azure) {
      (environment as any).azure = {};
    }
    (environment.azure as { backendUrl?: string }).backendUrl =
      'https://api.aiprofilephotomaker.com';
    environment.baseUrl = 'https://api.aiprofilephotomaker.com';

    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
    });

    service = TestBed.inject(StylePreviewService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.match(() => true).forEach(req => req.flush({ success: false, previews: [] }));
    httpMock.verify();
    (environment.azure as { backendUrl?: string }).backendUrl = originalBackendUrl;
    environment.baseUrl = originalBaseUrl;
  });

  it('uses API proxy URLs for style previews so private storage can render in production', () => {
    const url = service.getCachedUrl('tech professional');

    expect(url).toBe(
      'https://api.aiprofilephotomaker.com/profile-images/style-previews/tech-professional.jpg?v=20260110'
    );
  });
});

import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { HeadshotGenerationService } from './headshot-generation.service';
import { ConfigService } from './config.service';

describe('HeadshotGenerationService', () => {
  let service: HeadshotGenerationService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [HeadshotGenerationService, ConfigService],
    });

    service = TestBed.inject(HeadshotGenerationService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('posts to the provider-agnostic headshot generation endpoint', () => {
    const request = {
      imageStoragePath: 'dev/enhanced/user-1/source.png',
      style: 'professional',
      background: 'auto',
      numOutputs: 1,
      turnstileToken: 'token',
      clientRequestId: 'request-123',
    };

    service.generateHeadshot(request).subscribe(response => {
      expect(response.success).toBeTrue();
      expect(response.data?.provider).toBe('openai');
      expect(response.data?.imageUrl).toContain('/generated/');
    });

    const req = httpMock.expectOne('/api/headshots/generate');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);

    req.flush({
      success: true,
      data: {
        success: true,
        imageUrl: 'https://cdn.example.test/generated/headshot.png',
        storagePath: 'dev/generated/user-1/headshot.png',
        processedImageId: 123,
        provider: 'openai',
        model: 'gpt-image-2',
        style: 'professional',
        background: 'auto',
        creditsCost: 1,
        remainingCredits: 4,
        correlationId: 'instant_headshot_generation:abc',
      },
      error: null,
    });
  });
});

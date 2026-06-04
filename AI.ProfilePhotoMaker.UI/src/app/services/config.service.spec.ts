import { TestBed } from '@angular/core/testing';
import { ConfigService } from './config.service';

describe('ConfigService - API URL building in test env', () => {
  let service: ConfigService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(ConfigService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  describe('API URL building under Karma (proxy /api)', () => {
    it('should use /api base when no apiUrl is configured', () => {
      const baseUrl = service.baseUrl;
      expect(baseUrl).toBe('/api');
    });

    it('should build correct relative endpoint URLs', () => {
      expect(service.authLoginUrl).toBe('/api/auth/login');
      expect(service.profileUrl).toBe('/api/profile');
      expect(service.buildApiEndpoint('credit/status')).toBe('/api/credit/status');
      expect(service.profileTrainingStatusUrl).toBe('/api/model-creation/user/current');
      expect(service.imageListUrl).toBe('/api/image/images');
    });

    it('should buildEndpointUrl with relative /api base', () => {
      const endpoint = '/test/endpoint';
      const result = (service as any)['buildEndpointUrl'](endpoint);
      expect(result).toBe('/api/test/endpoint');
    });

    it('should report localhost environment under Karma', () => {
      const env = service.getCurrentEnvironment();
      // Karma host 9876 counts as localhost
      expect(env).toBe('localhost');
    });
  });

  describe('Runtime client flags', () => {
    let originalFetch: typeof window.fetch;

    beforeEach(() => {
      originalFetch = window.fetch;
    });

    afterEach(() => {
      window.fetch = originalFetch;
    });

    it('should override build-time feature flags from /api/config/client', async () => {
      window.fetch = jasmine.createSpy('fetch').and.resolveTo(
        new Response(
          JSON.stringify({
            success: true,
            data: {
              features: {
                openAIHeadshotMvp: false,
                replicateTrainingFlowVisible: false,
              },
            },
          }),
          { status: 200, headers: { 'Content-Type': 'application/json' } }
        )
      );

      await service.loadClientConfiguration();

      expect(window.fetch).toHaveBeenCalledWith('/api/config/client', jasmine.any(Object));
      expect(service.isOpenAIHeadshotMvpEnabled).toBeFalse();
      expect(service.isReplicateTrainingFlowVisible).toBeFalse();
    });
  });

  describe('OAuth Configuration', () => {
    it('should use window origin for OAuth in tests', () => {
      const oauthBase = service.getOAuthBaseUrl();
      expect(oauthBase).toBe(window.location.origin);
    });
  });

  describe('Static File URLs', () => {
    it('should build upload URLs with /api base in tests', () => {
      const uploadUrl = service.buildUploadUrl('test.jpg');
      expect(uploadUrl).toBe('/api/uploads/test.jpg');
    });

    it('should build generated image URLs with /api base in tests', () => {
      const generatedUrl = service.buildGeneratedImageUrl('result.png');
      expect(generatedUrl).toBe('/api/generated/result.png');
    });
  });
});

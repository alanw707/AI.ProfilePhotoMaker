import { TestBed } from '@angular/core/testing';
import { ConfigService } from './config.service';

describe('ConfigService - API Port Fix Validation', () => {
  let service: ConfigService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(ConfigService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  describe('API Port Configuration Fix Validation', () => {
    it('should use correct backend port 5032 for API calls', () => {
      const baseUrl = service.baseUrl;
      expect(baseUrl).toContain('5032');
      expect(baseUrl).toContain('http://localhost:5032/api');
    });

    it('should build correct endpoint URLs with port 5032', () => {
      const authUrl = service.authLoginUrl;
      expect(authUrl).toBe('http://localhost:5032/api/auth/login');

      const profileUrl = service.profileUrl;
      expect(profileUrl).toBe('http://localhost:5032/api/profile');

      const creditUrl = service.replicateCreditsUrl;
      expect(creditUrl).toBe('http://localhost:5032/api/credit/status');
    });

    it('should build dashboard API endpoints correctly', () => {
      const endpoints = {
        credits: service.replicateCreditsUrl,
        trainingStatus: service.profileTrainingStatusUrl,
        profile: service.profileUrl,
        images: service.imageListUrl,
      };

      expect(endpoints.credits).toBe('http://localhost:5032/api/credit/status');
      expect(endpoints.trainingStatus).toBe(
        'http://localhost:5032/api/model-creation/user/current'
      );
      expect(endpoints.profile).toBe('http://localhost:5032/api/profile');
      expect(endpoints.images).toBe('http://localhost:5032/api/image/images');
    });

    it('should handle buildEndpointUrl with full URLs correctly', () => {
      const endpoint = '/test/endpoint';
      const result = service['buildEndpointUrl'](endpoint);
      expect(result).toBe('http://localhost:5032/api/test/endpoint');
    });

    it('should not use proxy path for API calls', () => {
      const apiUrl = service.getApiUrl();
      expect(apiUrl).not.toBe('/api');
      expect(apiUrl).toContain('http://localhost:5032');
    });

    it('should detect localhost environment correctly', () => {
      const env = service.getCurrentEnvironment();
      expect(env).toBe('localhost');
    });
  });

  describe('OAuth Configuration', () => {
    it('should use localhost origin for OAuth in development', () => {
      const oauthBase = service.getOAuthBaseUrl();
      expect(oauthBase).toBe('http://localhost:4200');
    });
  });

  describe('Static File URLs', () => {
    it('should build upload URLs with correct backend port', () => {
      const uploadUrl = service.buildUploadUrl('test.jpg');
      expect(uploadUrl).toContain('5032');
    });

    it('should build generated image URLs with correct backend port', () => {
      const generatedUrl = service.buildGeneratedImageUrl('result.png');
      expect(generatedUrl).toContain('5032');
    });
  });
});

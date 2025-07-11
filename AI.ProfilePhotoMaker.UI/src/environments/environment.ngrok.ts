export const environment = {
  production: false,
  development: true,
  test: false,
  // Single domain setup - API calls are proxied through the same domain
  apiUrl: '/api',
  baseUrl: '',
  appBaseUrl: '',
  apiBaseUrl: '/api',
  frontendBaseUrl: '',
  authLoginUrl: '/api/auth/login',
  authRegisterUrl: '/api/auth/register',
  authProfileCompletionUrl: '/api/auth/profile-completion-status',
  profileUrl: '/api/profile',
  uploadPhotoUrl: '/api/image/upload-photo',
  generateImageUrl: '/api/image/generate-profile-picture',
  generateSamplesUrl: '/api/image/generate-samples',
  userImagesUrl: '/api/image/user-images',
  configEndpoint: '/api/config/client',
  enableAutoUrlDetection: true,
  enableExternalAccess: true,
  oauth: {
    useExternalUrls: true
  }
};
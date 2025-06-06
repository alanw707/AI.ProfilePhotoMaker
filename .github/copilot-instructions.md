# Copilot Instructions for AI.ProfilePhotoMaker

## Project Overview
This project is an AI-powered profile photo generator with a .NET 8 Web API backend and an Angular frontend. The backend handles authentication, user management, and image processing. The application will leverage a 3rd party AI service API Replicate.com with Flux AI Model

## Coding Guidelines
- Use C# 12 and .NET 8 features where appropriate.
- Follow SOLID principles and clean architecture.
- Use dependency injection for all services.
- DTOs should be used for all API input/output.
- Validate all incoming API models and return meaningful error responses.
- Use async/await for all I/O operations.
- Write XML comments for all public methods and classes.

## API Design
- All controllers should inherit from `ControllerBase` and use `[ApiController]`.
- Use attribute routing (e.g., `[Route("api/[controller]")]`).
- Return IActionResult or ActionResult<T> for API endpoints.
- Use consistent response formats for success and error cases.
- All API responses should use a standard envelope:
  ```json
  {
    "success": true/false,
    "data": {},
    "error": {
      "code": "",
      "message": ""
    }
  }
  ```
- Document error response structure and HTTP status codes in Swagger/OpenAPI.

## Security
- Use JWT authentication for all protected endpoints.
- Never log or expose sensitive information.
- Implement rate limiting for authentication endpoints.
- Secure webhook endpoints (e.g., signature verification).
- Ensure secure storage and transmission of uploaded images.

## Frontend
- Use Angular best practices (components, services, modules).
- Use SASS for styling.
- Handle API errors gracefully and provide user feedback.
- Use Angular Reactive Forms for user input.
- Follow Angular style guide for file/folder structure.

## Testing
- Write unit and integration tests for backend services and controllers.
- Use Angular's testing tools for frontend components and services.
- Strive for at least 80% code coverage.
- Mock external AI API calls in tests.
- Test scheduled/automated cleanup jobs for data retention.

## Main Features

- User registration and authentication (JWT-based)
- AI-powered profile photo generation and editing
- User profile management
- Image upload, processing, and download
- Activity logging and audit trails
- Responsive Angular frontend for user interaction

## Application Workflow

- User can provide some basic information, like Gender, Ethnicity
- User can select up to 10 different profile photo styles
- User can then upload up to 10 selfie images to our application
- The application will zip the images uploaded and send to Replicate.com API to train a custom model per user
- Once the user custom model is done training, a webhook will notify the application and we'll send the basic user information along with selected style to generate the images
- Application will store and present the generated images for our user and allow them to download it
- Application will charge the user per transaction
- Application will delete the generated images after 7 days
- Application will delete the custom model after 24 hours

## AI Service Integration
- Document and implement webhook security (e.g., signature verification).
- Handle failures or retries from the AI service gracefully.

## Data Retention
- Automate and test cleanup jobs for deleting generated images and custom models according to retention policy.
- Document where and how data retention is implemented.


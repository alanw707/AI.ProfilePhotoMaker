# AI.ProfilePhotoMaker Task List

This document tracks actionable tasks for the development of the AI.ProfilePhotoMaker application. Tasks are grouped by feature area and mapped to the project plan milestones.

---

## Core Infrastructure
- [x] Initialize .NET 8 Web API project
- [x] Initialize Angular frontend project
- [ ] Set up CI/CD pipelines (GitHub Actions, Azure DevOps, etc.)
- [x] Configure JWT authentication in backend
- [x] Set up database and run initial migrations

## User Management & Authentication
- [x] Implement user registration endpoint
- [x] Implement user login endpoint
- [ ] Add user profile CRUD endpoints
- [x] Add validation for registration and profile updates
- [ ] Implement rate limiting for authentication endpoints

## Profile Photo Styles
- [ ] Define available profile photo styles (DB or static config)
- [ ] Create API endpoint to fetch available styles
- [ ] Create API endpoint to save user-selected styles (max 10)
- [ ] Integrate style selection in Angular UI

## Image Upload & Processing
- [ ] Implement image upload endpoint (max 10 images per user)
- [ ] Validate file type, size, and count
- [ ] Store uploaded images securely
- [ ] Zip images for AI processing

## AI Service Integration
- [x] Integrate with Replicate.com API for model training
- [x] Implement webhook endpoint for model training completion
- [x] Secure webhook with signature verification
- [x] Trigger image generation for selected styles

## Generated Image Management
- [x] Store generated images and metadata in database
- [ ] Create API endpoint to list generated images for user
- [ ] Create API endpoint to download generated images
- [ ] Enforce data retention (delete after 7 days)

## Payments & Transactions
- [ ] Integrate payment provider (e.g., Stripe)
- [ ] Require payment before image generation/download
- [ ] Track transactions per user

## Audit Logging
- [ ] Implement activity logging for key user actions
- [ ] Expose logs to admins (optional)

## Automated Cleanup & Data Retention
- [ ] Implement background job for deleting generated images after 7 days
- [ ] Implement background job for deleting custom models after 24 hours
- [ ] Test and document retention logic

## Frontend (Angular)
- [x] Build registration, login, and profile management pages (scaffolded)
- [ ] Build style selection and image upload UI
- [ ] Build payment and checkout UI
- [ ] Display generated images and enable downloads
- [ ] Handle API errors and provide user feedback

## Testing & QA
- [ ] Write unit tests for backend services and controllers
- [ ] Write integration tests for backend APIs
- [ ] Write Angular component and service tests
- [ ] Mock external AI and payment APIs in tests
- [ ] Achieve at least 80% code coverage

## Documentation
- [x] Document all API endpoints and error responses in Swagger
- [ ] Update README and user guides

---

*Update this list as tasks are completed or new requirements are discovered.*

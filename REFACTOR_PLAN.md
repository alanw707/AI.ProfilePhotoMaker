# Backend Refactoring and Improvement Plan

This document outlines a comprehensive plan to refactor the `AI.ProfilePhotoMaker.API` backend. The goal is to improve code quality, maintainability, testability, and scalability by adhering to modern software architecture best practices.

---

## Guiding Principles

*   **Single Responsibility Principle (SRP):** Each class and method should have one, and only one, reason to change.
*   **Separation of Concerns:** Decouple different areas of the application (e.g., presentation, business logic, data access).
*   **Dependency Inversion:** Depend on abstractions, not on concrete implementations.
*   **Configuration Management:** Externalize all environment-specific settings and secrets.

---

## Phase 1: Controller and Service Layer Refactoring

This phase focuses on breaking down large classes and separating business logic from the presentation layer.

### 1.1. Decompose `ProfileController` into Focused Controllers

**Problem:** The current `ProfileController` is a "God" controller, handling too many unrelated responsibilities.

**Plan:**
- **Create a new `ImageController`:**
  - **Responsibility:** Handle all image-related operations.
  - **Actions to Move:** `UploadImages`, `GetImages`, `DeleteImage`, `GetDataStats`, `DeleteInputPhotos`.
- **Create a new `TrainingController`:**
  - **Responsibility:** Manage the AI model training lifecycle.
  - **Actions to Move:** `CreateTrainingZip`, `GetTrainingZips`, `GetLatestTrainingZip`, `DeleteTrainingZip`, `DeleteAllTrainingZips`, `GetTrainingStatus`, `CheckModelStatus`, `DeleteAIModel`.
- **Create a new `GenerationController`:**
  - **Responsibility:** Handle all AI image generation requests.
  - **Actions to Move:** `GenerateImages`, `GenerateBasicImage`.
- **Slim down the existing `ProfileController`:**
  - **Responsibility:** Manage only user profile data (CRUD).
  - **Actions to Keep:** `GetProfile`, `CreateProfile`, `UpdateProfile`, `DeleteProfile`.

### 1.2. Abstract Business Logic into Services

**Problem:** Business logic (e.g., file validation, ZIP creation) is tightly coupled within controller actions.

**Plan:**
- **Create `IFileStorageService`:**
  - **Responsibility:** Abstract all file system operations (saving, deleting, URL generation). This is critical for supporting Azure Blob Storage in production.
  - **Implementations:** `LocalStorageService` (for development), `AzureBlobStorageService` (for production).
- **Create `IImageValidationService`:**
  - **Responsibility:** Centralize the validation of uploaded image files (size, type, magic bytes).
  - **Action:** Move the `IsValidImageFile` logic from `ProfileController` into this service.
- **Create `IWebhookProcessingService`:**
  - **Responsibility:** Handle the business logic of processing incoming webhooks from Replicate.
  - **Action:** Move the core logic from `ReplicateWebhookController` into this service, leaving the controller as a thin entry point.

---

## Phase 2: AI Integration and Configuration

This phase focuses on decoupling the application from the specific implementation details of the Replicate API and improving configuration management.

### 2.1. Externalize Configuration

**Problem:** The `ReplicateApiClient` contains hardcoded values (e.g., model owner, model IDs).

**Plan:**
- **Create a strongly-typed `ReplicateApiSettings` class.**
- **Move all Replicate-specific values** (`ApiToken`, `FluxTrainingModelId`, `owner`, etc.) from the code into `appsettings.json` and bind them to the `ReplicateApiSettings` object.
- **Inject `IOptions<ReplicateApiSettings>`** into services that need these values.

### 2.2. Separate Prompt Generation Logic

**Problem:** The logic for creating AI prompts is mixed with the API client code.

**Plan:**
- **Create a new `IPromptGenerationService`:**
  - **Responsibility:** Generate the detailed, structured prompts for the AI model based on style, user info, and other business rules.
  - **Action:** Move the `CreateFluxStylePrompt` and related methods from `ReplicateApiClient` into this new service.
- **Refactor `ReplicateApiClient`:**
  - Its sole responsibility should be making HTTP requests to the Replicate API and deserializing responses. It will now receive the fully-formed prompt from the `IPromptGenerationService`.

---

## Phase 3: Data Layer and Database Improvements

This phase focuses on improving data integrity and making the database schema more robust and manageable.

### 3.1. Improve Data Seeding Strategy

**Problem:** The `ApplicationDbContext` contains long, hardcoded strings for seeding the `Style` entity, which are difficult to manage.

**Plan:**
- **Move `Style` seed data to an external file** (e.g., `Styles.json`).
- **Update the database seeding process** to read from this JSON file during migrations. This makes it much easier to add, remove, or update styles without changing C# code.

### 3.2. Enforce Data Integrity with Cascade Deletes

**Problem:** The database does not have explicit rules for handling the deletion of related data, which could lead to orphaned records.

**Plan:**
- **Configure explicit cascade deletes** in `ApplicationDbContext.OnModelCreating`.
- **Example:** When a `UserProfile` is deleted, ensure all its associated `ProcessedImages`, `UserStyleSelections`, and `UsageLogs` are automatically removed from the database by adding `.OnDelete(DeleteBehavior.Cascade)` to the entity relationships.

---

## Implementation Order

1.  **Start with Service Abstractions:** Create `IFileStorageService`, `IImageValidationService`, and `IPromptGenerationService` interfaces.
2.  **Refactor `ReplicateApiClient`:** Externalize its configuration and inject the new `IPromptGenerationService`.
3.  **Decompose `ProfileController`:** Create the new controllers (`ImageController`, `TrainingController`, `GenerationController`) and move the actions, injecting the new services as needed.
4.  **Refactor Webhooks:** Create the `WebhookProcessingService` and clean up the `ReplicateWebhookController`.
5.  **Update Data Layer:** Implement the improved data seeding strategy and configure cascade deletes in the `ApplicationDbContext`.

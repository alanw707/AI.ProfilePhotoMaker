### **UI Refactoring: Detailed Task Plan**

The goal of this plan is to systematically address the remaining technical debt identified in the `AI.ProfilePhotoMaker.UI` project, focusing on one area at a time.

### Phase 1: Decompose `SettingsComponent` (Est. 5 tasks)

**Goal:** Reduce the `SettingsComponent` from a 434-line monolith into a simple container that orchestrates smaller, focused child components.

*   `[ ]` **Task 1.1: Create Baseline Tests.**
    *   Before making changes, create a `settings.component.spec.ts` file.
    *   Write a few high-level tests that confirm the component renders and the main features (profile form, security section) are present. This will serve as a safety net.

*   `[ ]` **Task 1.2: Extract Profile Details Form.**
    *   Generate a new component: `ng generate component components/settings/profile-details-form`.
    *   Move the HTML form for editing user details (first name, last name, etc.) from `settings.component.html` into the new component's template.
    *   Move the corresponding form logic (`FormGroup`, submission handlers) from `settings.component.ts` into the new component.
    *   Use `@Input` and `@Output` to pass data and events between the `SettingsComponent` and the new child component.
    *   Update the `SettingsComponent` template to use `<app-profile-details-form>`.

*   `[ ]` **Task 1.3: Extract Account Security Logic.**
    *   Generate a new component: `ng generate component components/settings/account-security`.
    *   Move the HTML and logic for the "Change Password" feature into this new component.
    *   Wire it up within the main `SettingsComponent` container.

*   `[ ]` **Task 1.4: Extract Subscription Management.**
    *   Generate a new component: `ng generate component components/settings/subscription-management`.
    *   Move the UI and logic related to displaying the user's current subscription and providing a link to the Stripe customer portal into this new component.
    *   Integrate the new component into the `SettingsComponent` container.

*   `[ ]` **Task 1.5: Final Cleanup.**
    *   After all logic has been extracted, review `settings.component.ts`.
    *   Remove any unused injected services, private methods, or properties, leaving only the orchestration logic.

### Phase 2: Consolidate Gallery Components (Est. 2-3 tasks)

**Goal:** Investigate the duplicate `GalleryComponent` and `PhotoGalleryComponent` and eliminate any redundant code.

*   `[ ]` **Task 2.1: Investigate `GalleryComponent` Usage.**
    *   Perform a project-wide search for the selector `app-gallery`.
    *   Check all routing modules (`app-routing.module.ts`, etc.) to see if a route points to `GalleryComponent`.
    *   **Deliverable:** A clear determination of whether `GalleryComponent` is obsolete or still in use.

*   `[ ]` **Task 2.2 (If Obsolete): Safe Deletion.**
    *   If the component is not used, delete the following files:
        *   `gallery.component.ts`
        *   `gallery.component.html`
        *   `gallery.component.sass`
        *   `gallery.component.spec.ts`
    *   Remove it from any `@NgModule` declarations.

*   `[ ]` **Task 2.3 (If In Use): Refactor to Reuse Components.**
    *   If the component is in use, refactor its template to use the new, smaller gallery components:
        *   `<app-gallery-filter-controls>`
        *   `<app-gallery-image-actions>`
        *   `<app-gallery-pagination>`
    *   Remove all duplicated logic from `gallery.component.ts` and delegate functionality to the child components via inputs and outputs, mirroring the structure of the refactored `PhotoGalleryComponent`.

### Phase 3: Decompose `WorkflowOrchestrationService` (Est. 3-4 tasks)

**Goal:** Break down the large `WorkflowOrchestrationService` (617 lines) into smaller, more manageable services based on its responsibilities.

*   `[ ]` **Task 3.1: Analyze and Plan Service Split.**
    *   This is a planning task. Carefully read through all methods in `WorkflowOrchestrationService`.
    *   Group methods by their domain: which methods are purely for model training? Which are for image generation? Which are for state polling?
    *   **Deliverable:** A plan that defines the names and responsibilities of the new, smaller services to be created.

*   `[ ]` **Task 3.2: Extract `TrainingWorkflowService`.**
    *   Create a new service named `TrainingWorkflowService`.
    *   Move all methods and properties related to the model training process from `WorkflowOrchestrationService` into this new service.
    *   Update the `DashboardComponent` and any other consumers to inject and use this new, more focused service.

*   `[ ]` **Task 3.3: Extract `GenerationWorkflowService`.**
    *   Create a new service named `GenerationWorkflowService`.
    *   Move all methods and properties related to the photo generation process into this new service.
    *   Update all consumers to use this new service.

*   `[ ]` **Task 3.4: Cleanup `WorkflowOrchestrationService`.**
    *   After the logic has been moved, review the original `WorkflowOrchestrationService`.
    *   Determine if its remaining responsibilities are still valid or if it can be eliminated entirely in favor of the new, more granular services.

### Phase 4: Backend API Refactoring (Est. 10+ tasks)

**Goal:** Decompose large controllers and services, standardize API responses, and improve code quality.

*   `[ ]` **Task 4.1: Analyze `BaseController` for API Response Standardization.**
    *   Read `AI.ProfilePhotoMaker.API/Controllers/BaseController.cs`.
    *   Determine if `SuccessResponse` and `ErrorResponse` methods already return a consistent structure.
    *   **Deliverable:** Decision on whether to create a new `ApiResponse<T>` class or just modify existing methods.

*   `[ ]` **Task 4.2: Define `ApiResponse<T>` and `ApiError` (if needed).**
    *   Create new classes `ApiResponse.cs` and `ApiError.cs` in a common `Models/Responses` directory.
    *   Define their properties (e.g., `IsSuccess`, `Data`, `Error`).

*   `[ ]` **Task 4.3: Update `BaseController` to use `ApiResponse<T>`.**
    *   Modify `SuccessResponse` and `ErrorResponse` methods in `BaseController.cs` to return instances of `ApiResponse<T>`.
    *   Ensure all existing controller actions that inherit from `BaseController` still compile and function correctly.

*   `[ ]` **Task 4.4: Create New Controllers.**
    *   Generate new controller files: `ModelController.cs`, `GenerationController.cs`.
    *   Ensure they inherit from the `BaseController`.

*   `[ ]` **Task 4.5: Move Actions to `ModelController`.**
    *   Identify and move all actions related to AI model training (e.g., initiating training, checking training status) from `ProfileManagementController` and `ReplicateWebhookController` to `ModelController.cs`.
    *   Update routing and any internal calls.

*   `[ ]` **Task 4.6: Move Actions to `GenerationController`.**
    *   Identify and move all actions related to AI image generation (e.g., generating images, checking prediction status) from `ImageController` and `ReplicateWebhookController` to `GenerationController.cs`.
    *   Update routing and any internal calls.

*   `[ ]` **Task 4.7: Create `IReplicateModelService` and `ReplicateModelService`.**
    *   Define interface `IReplicateModelService.cs` and implementation `ReplicateModelService.cs`.
    *   Move model-related methods (e.g., `CreateModelAsync`, `CreateModelTrainingAsync`, `GetTrainingStatusAsync`, `CheckModelExistsAsync`, `DeleteModelAsync`) from `ReplicateApiClient.cs` to `ReplicateModelService.cs`.
    *   Update `Program.cs` for dependency injection.

*   `[ ]` **Task 4.8: Create `IReplicatePredictionService` and `ReplicatePredictionService`.**
    *   Define interface `IReplicatePredictionService.cs` and implementation `ReplicatePredictionService.cs`.
    *   Move prediction-related methods (e.g., `GenerateImagesAsync`, `GetPredictionStatusAsync`, `EnhancePhotoAsync`, `GenerateBasicImageAsync`) from `ReplicateApiClient.cs` to `ReplicatePredictionService.cs`.
    *   Update `Program.cs` for dependency injection.

*   `[ ]` **Task 4.9: Create `IPromptGenerationService` and `PromptGenerationService`.**
    *   Define interface `IPromptGenerationService.cs` and implementation `PromptGenerationService.cs`.
    *   Move prompt-related methods (e.g., `GetStylePromptsFromDatabase`, `CreateFluxStylePrompt`, `CreateFluxStylePromptBasic`, `GetSubjectDescription`, `GetEnhancementPrompt`, `GetRandomSocialMediaPrompt`) from `ReplicateApiClient.cs` to `PromptGenerationService.cs`.
    *   Consolidate `CreateFluxStylePrompt` and `CreateFluxStylePromptBasic` into a single method with a parameter.
    *   Update `Program.cs` for dependency injection.

*   `[ ]` **Task 4.10: Create `IZipService` and `ZipService`.**
    *   Define interface `IZipService.cs` and implementation `ZipService.cs`.
    *   Move `CreateTrainingZip` method from `ImageController.cs` to `ZipService.cs`.
    *   Update `Program.cs` for dependency injection.

*   `[ ]` **Task 4.11: Clean Up `ReplicateApiClient`.**
    *   Remove all moved methods from `ReplicateApiClient.cs`.
    *   Ensure it only contains core HTTP client setup and potentially very generic API calls if any remain.

*   `[ ]` **Task 4.12: Externalize Magic Strings.**
    *   Identify hardcoded strings (e.g., "Original" style, "user_" prefix, frontend URL `http://localhost:4200`).
    *   Move them to `appsettings.json` or a `Constants.cs` file.
    *   Update all references to use the new configuration/constants.
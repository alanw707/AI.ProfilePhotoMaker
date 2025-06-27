# Gemini Project Instructions

This document provides project-specific instructions and context for the Gemini agent. It is the single source of truth for project status, architecture, and business logic.

---

## Project Status (June 2025)

*   **Overall**: The project foundation is strong, with a feature-rich backend and an early-stage frontend. The immediate focus is on completing the frontend UI, implementing payment processing, and preparing for production deployment.
*   **Backend**: Substantially complete. Core services for authentication, image processing, and AI integration are functional. Key remaining tasks include Stripe integration and finalizing background services.
*   **Frontend**: Early development. The basic Angular structure is in place, but UI components for most features still need to be built.

---

## Core Business Logic: The Credit System

The application uses a unified credit system to manage user access to AI operations.

### Credit Types

1.  **Weekly Credits (Basic Tier):**
    *   **Amount:** Users receive 3 credits every 7 days.
    *   **Usage:** Can only be used for **Photo Enhancement** operations.
    *   **Management:** Managed by `BasicTierService` and `BasicTierBackgroundService`. The background service runs hourly to check for users whose `LastCreditReset` date is older than 7 days.
    *   **Storage:** Tracked in the `UserProfile.Credits` and `UserProfile.LastCreditReset` fields.

2.  **Purchased Credits (Premium Tier):**
    *   **Usage:** Required for premium features like **Model Training** and **Styled Generation**.
    *   **Management:** Handled by `CreditPackageService`.
    *   **Storage:** Credits are permanent until consumed and are tracked via the `CreditPurchase` and `UsageLog` tables.

### Credit Costs

| Operation | Cost | Required Credit Type | AI Model Used |
| :--- | :--- | :--- | :--- |
| Photo Enhancement | 1 credit | Weekly or Purchased | `flux-kontext-pro` |
| Model Training | 15 credits | Purchased Only | `fast-flux-trainer` |
| Styled Generation | 5 credits | Purchased Only | `flux-dev` (with custom model) |

---

## Architecture & Technology

### Project Structure
-   **`AI.ProfilePhotoMaker.API/`**: The .NET 8 Web API backend.
-   **`AI.ProfilePhotoMaker.UI/`**: The Angular 19 frontend.

### Backend Details (`AI.ProfilePhotoMaker.API`)
*   **Technology**: ASP.NET Core 8.0, Entity Framework Core
*   **Database**: SQLite (Development), SQL Server (Production)
*   **Authentication**: JWT, ASP.NET Core Identity, OAuth (Google, Facebook, Apple)
*   **Key Services**:
    *   `BasicTierService`: Manages the weekly credit system.
    *   `CreditPackageService`: Manages purchased credit packages.
    *   `ReplicateApiClient`: Handles all communication with the Replicate.com API.
    *   `RetentionPolicyBackgroundService`: Handles cleanup of old images and models.

### Frontend Details (`AI.ProfilePhotoMaker.UI`)
*   **Technology**: Angular 19, TypeScript
*   **Styling**: SASS (`.sass` files)
*   **Key Libraries**:
    *   `face-api.js`: For client-side face detection before upload.
    *   `jszip`: For client-side image processing/zipping if needed.
    *   `@abacritt/angularx-social-login`: For handling social logins.

---

## Key Workflows

### 1. Premium Tier: Custom Model Training & Generation
1.  **Upload:** User uploads 5-20 high-quality selfies. `face-api.js` validates images on the client.
2.  **Training:** The backend zips the images and sends them to Replicate.com to train a custom model using `fast-flux-trainer`. This costs **15 purchased credits**.
3.  **Webhook (Training):** A Replicate webhook notifies the backend upon training completion. The user's profile is updated with the new model ID.
4.  **Generation:** The user selects from various styles. The backend uses the custom model with `flux-dev` to generate new images. This costs **5 purchased credits** per generation.
5.  **Webhook (Generation):** Another webhook provides the URL of the generated image, which is then saved to the user's profile.

### 2. Basic Tier: Photo Enhancement
1.  **Upload:** User uploads a single photo.
2.  **Enhancement:** The backend sends the image and a text-based prompt to Replicate's `flux-kontext-pro` model for enhancement (e.g., "Remove background", "Transform into a cartoon").
3.  **Credit Deduction:** This costs **1 credit** (weekly or purchased).
4.  **Result:** The enhanced image URL is saved to the user's profile.

---

## Development & Deployment

### Common Commands
*   **Run API**: `dotnet run --project AI.ProfilePhotoMaker.API/AI.ProfilePhotoMaker.API.csproj`
*   **Run UI**: `cd AI.ProfilePhotoMaker.UI && npm start`
*   **Run API Tests**: `dotnet test`
*   **Run UI Tests**: `cd AI.ProfilePhotoMaker.UI && npm test`

### API Configuration (`appsettings.Development.json`)
The application requires the following configuration keys. For development, secrets should be stored in the .NET Secret Manager.
```json
{
  "ConnectionStrings": { "DefaultConnection": "..." },
  "JWT": { "ValidAudience": "...", "ValidIssuer": "...", "Secret": "..." },
  "Replicate": {
    "ApiToken": "...",
    "WebhookSecret": "...",
    "FluxTrainingModelId": "replicate/fast-flux-trainer:...",
    "FluxGenerationModelId": "black-forest-labs/flux-dev",
    "FluxKontextProModelId": "black-forest-labs/flux-kontext-pro"
  },
  "AppBaseUrl": "https://<your-ngrok-url>"
}
```

### Production Readiness Plan
*   **Infrastructure**: Provision Azure SQL for the database and Azure Blob Storage for file storage.
*   **Configuration**: Use Azure Key Vault for all secrets in production.
*   **CI/CD**: Enhance the GitHub Actions workflow (`.github/workflows/dotnet.yml`) to build the Angular UI and deploy both the frontend and backend to a hosting service like Azure App Service.
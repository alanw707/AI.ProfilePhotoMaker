# Copilot Instructions for AI.ProfilePhotoMaker

## Project Overview
AI.ProfilePhotoMaker is a full-stack application for generating professional profile photos using AI. Users upload selfies, train custom models via Replicate.com FLUX API, and generate styled photos.

**Tech Stack:**
- Backend: .NET 8 Web API (Entity Framework Core, ASP.NET Identity, JWT)
- Frontend: Angular 19 (TypeScript, SASS)
- Database: SQLite (dev), SQL Server (prod)
- AI: Replicate.com FLUX models
- Storage: Local filesystem (Azure Blob planned)

## Key Folders
- `AI.ProfilePhotoMaker.API/` — .NET backend (controllers, services, models, migrations)
- `AI.ProfilePhotoMaker.UI/` — Angular frontend (components, services, assets)
- `docs/` — Architecture, setup, troubleshooting
- `.github/` — Project documentation

## Common Commands
### Backend
```bash
cd AI.ProfilePhotoMaker.API
dotnet restore
dotnet build
dotnet run
# Migrations
dotnet ef migrations add MigrationName
dotnet ef database update
```
### Frontend
```bash
cd AI.ProfilePhotoMaker.UI
npm install
ng serve
ng build
ng test
```

## Key Features & Workflows
- **Selfie Upload:** Users upload 5–20 high-quality selfies (min 1024x1024, face detected via face-api.js)
- **Image Deletion:** Users can delete uploaded images before and after upload
- **Model Training:** Images zipped and sent to Replicate for model training
- **Styled Generation:** User selects styles, generates photos using trained model
- **Basic Tier:** 3 free credits/week, basic generation/enhancement (no training)
- **Premium:** Paid credits, premium features (in progress)
- **Credit System:** Tracks usage, resets weekly

## Integrations
- **Replicate.com:** Model training, image generation, enhancement
- **Ngrok:** For local webhook testing
- **face-api.js:** Client-side face detection (models in `src/assets/models/`)

## Development Guidelines
- Use correct terminology: "Basic tier" (not "Free tier"), "Credits" (not "FreeCredits")
- Update database via EF migrations when models change
- Use JWT for protected endpoints
- CORS: Allow all in dev
- Use `.gitignore` to avoid committing DB, uploads, ngrok, lock files

## Testing & Debugging
- Use Swagger at `/swagger` for API testing
- Use ngrok for webhook testing
- Use Angular dev server on port 4200 (free port if needed)
- Download face-api.js models for local face detection

## Recent Changes
- Face detection and image quality checks on upload (Angular, face-api.js)
- Image deletion in upload step and gallery
- Replicate API/webhook fixes
- Credit system and terminology updates
- Premium features branch started

## References
- See `docs/` for architecture, setup, OAuth troubleshooting, and project plan
- See `.github/` for additional documentation

---
**For Copilot/AI:**
- Follow these conventions and workflows for all code changes
- Reference this file for onboarding and future enhancements
- Keep terminology and workflows consistent with this document

# AI Profile Photo Maker

Create professional headshots with AI. This monorepo contains the .NET 8 Web API, Angular 19 frontend, deployment scripts, and reference documentation that power **AI Profile Photo Maker**.

> 📚 Looking for docs? Jump straight to the [Documentation Hub](docs/INDEX.md).

## 🔗 Quick Links

| Area | Description |
|------|-------------|
| 🗂️ [Documentation Hub](docs/INDEX.md) | Architecture, deployment, operations, and security guides |
| 🚀 [Local Build Workflow](docs/LOCAL-BUILD-WORKFLOW.md) | Step-by-step guide for the local container build + deploy flow |
| 🛡️ [Security Notes](docs/security/SECURITY_NOTES.md) | Logging hygiene, secrets, and controller protections |
| 🧪 [Test Strategy](docs/development/TEST_ANALYSIS_REPORT.md) | Coverage overview and regression plan |
| 🗺️ [Project Plan](docs/development/PROJECT_PLAN.md) | Milestones, roadmap, and sprint planning |

## 🏗️ Architecture Overview

- **Frontend:** Angular 19 SPA hosted on Azure Container Apps. Responsive UI with modular feature areas and reusable components.
- **Backend:** ASP.NET Core (.NET 8) Web API with EF Core, ASP.NET Identity, and JWT authentication. Integrates with Replicate FLUX for model training and Stripe for payments.
- **Storage Modes:** Supports both public blob delivery and a secure API proxy mode for private containers (`Storage:ProxyBlobRequests`).
- **Infrastructure:** Azure Container Apps + Azure SQL + Azure Storage + Azure Key Vault + Application Insights. Deployment helpers live under [`scripts/`](scripts/).

More detail (including diagrams and decision records) lives in [docs/architecture/ARCHITECTURE_OVERVIEW.md](docs/architecture/ARCHITECTURE_OVERVIEW.md).

## ⚡ Getting Started (Developers)

```bash
# Clone the repo
 git clone https://github.com/alanw707/AI.ProfilePhotoMaker.git
 cd AI.ProfilePhotoMaker

# Backend
 cd AI.ProfilePhotoMaker.API
 dotnet restore
 dotnet run

# Frontend (new terminal)
 cd ../AI.ProfilePhotoMaker.UI
 npm install
 ng serve

# App available at http://localhost:4200
```

Environment setup, secrets, OAuth configuration, and troubleshooting tips are documented in [docs/ENVIRONMENT_SETUP.md](docs/ENVIRONMENT_SETUP.md).

## 🧰 Local Build & Deploy Workflow

This project favors a “build locally, push once” workflow for reproducible deployments.

```bash
./scripts/build-local.sh     # Build backend + frontend images locally
./scripts/push-to-acr.sh     # Push images to Azure Container Registry
git push origin main         # GitHub Actions deploy the freshly pushed images
```

Additional helpers (credential sync, rollbacks, diagnostics) are available in [`scripts/`](scripts/).

## ✅ Testing & Quality

- **API:** `dotnet test AI.ProfilePhotoMaker.API.Tests/AI.ProfilePhotoMaker.API.Tests.csproj --configuration Release`
- **UI:** `npm run test` inside `AI.ProfilePhotoMaker.UI`
- **E2E:** Playwright scenarios live under `tests/e2e/`

CI runs CodeQL, static analysis, and targeted regression suites on every PR.

## 📁 Repository Layout

```
AI.ProfilePhotoMaker/
├── AI.ProfilePhotoMaker.API/         # ASP.NET Core Web API
├── AI.ProfilePhotoMaker.API.Tests/   # API unit & integration tests
├── AI.ProfilePhotoMaker.UI/          # Angular SPA
├── docs/                             # Architecture, deployment, operations docs
├── scripts/                          # Local build/deploy helpers
├── tests/                            # Playwright & performance suites
└── README.md                         # You are here
```

## 🤝 Contributing

1. Fork the repo & create a feature branch.
2. Run lint/tests locally (`dotnet test`, `npm run lint`).
3. Open a PR against `main`.

Please follow the logging hygiene guidelines in [SECURITY_NOTES](docs/security/SECURITY_NOTES.md#logging-hygiene) when touching API code.

## 📄 License

Distributed under the MIT License. See [LICENSE.txt](LICENSE.txt) for details.

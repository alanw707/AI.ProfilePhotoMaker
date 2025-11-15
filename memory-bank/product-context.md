# Project Overview

## Description
AI Profile Photo Maker is a consumer-facing platform that turns user-uploaded selfies into professional headshots. The monorepo hosts the .NET 8 Web API (training orchestration, storage, payments) plus the Angular 19 SPA (upload workflow, gallery, credits) alongside docs and infra automation.

## Objectives
- Deliver a reliable end-to-end AI workflow: upload, model training (Replicate FLUX), styled generation, gallery downloads.
- Offer a polished self-serve UX with authentication, dashboards, and real-time state healing between DB + filesystem.
- Monetize through Stripe-powered credit packages while enforcing quotas/retention for cost control.

## Technologies
- Backend: ASP.NET Core (.NET 8), EF Core, ASP.NET Identity, SQL Server, SignalR, Azure Storage.
- AI/Payments: Replicate FLUX (training + generation) and Stripe Payment Intents + webhooks.
- Frontend: Angular 19 SPA with SASS, feature modules, responsive grid layout, Cypress/Playwright for E2E.

## Architecture
- Angular UI served via Azure Container Apps; communicates with REST API on 5032 locally (ngrok optional for callbacks).
- API coordinates uploads, background jobs (training polling, retention, credit resets) and stores blobs in Azure/Azurite; storage proxy option for private delivery.
- Automation scripts (`scripts/`, `dev-start.sh`) spin up SQL/Azurite containers plus API/UI dev servers; CI/CD handled via GitHub Actions + container builds.

## Project Structure
- `AI.ProfilePhotoMaker.API/`: controllers, services, EF models, configuration/extensions, SignalR hubs, Scripts & tests/playwright for API validation.
- `AI.ProfilePhotoMaker.API.Tests/`: xUnit suites (unit, integration, controller, infrastructure, performance) with fixtures/builders.
- `AI.ProfilePhotoMaker.UI/`: Angular 19 app with feature modules, shared libs, assets, Cypress + Playwright harnesses.
- `docs/`, `tests/`, `scripts/`, `infrastructure/`: documentation hub, combined Playwright scenarios, automation utilities, and IaC assets.

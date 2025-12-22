# Project Overview

## Purpose
AI Profile Photo Maker provides a full-stack experience for generating and enhancing profile photos using external AI model providers.

## Executive Summary
Monorepo with two primary parts: an ASP.NET Core backend API and an Angular web UI. The API handles authentication, payments, model training/inference, and storage. The UI provides onboarding, style selection, and gallery workflows.

## Tech Stack Summary
| Part | Language | Framework | Data | Notes |
| --- | --- | --- | --- | --- |
| API | C# (.NET 8) | ASP.NET Core | EF Core + SQL Server | Stripe, Replicate/OpenAI, Azure Blob |
| UI | TypeScript | Angular 19 | Client-side models | Stripe JS, face-api.js |

## Architecture Classification
- Repository Type: Monorepo (multi-part)
- API: Layered Web API
- UI: Angular standalone configuration + feature components

## Quick Links
- API Architecture: `docs/architecture-api.md`
- UI Architecture: `docs/architecture-ui.md`
- Source Tree: `docs/source-tree-analysis.md`
- Development Guide: `docs/development-guide.md`
- Deployment Guide: `docs/deployment-guide.md`
- Integration Architecture: `docs/integration-architecture.md`

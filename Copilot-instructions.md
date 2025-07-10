# Copilot-instructions.md

This file provides guidance to GitHub Copilot and other AI coding assistants when working with code in this repository. It is inspired by the workflow and structure in `CLAUDE.md` and is intended for Copilot and similar AI tools.

## Standard Workflow

1. **Think before you code**: Carefully read the codebase and relevant files before making changes. Write a plan for any significant refactor or feature in `docs/[task]_tasks.md`.
2. **Task Planning**: For each major change, create a checklist of todo items. Keep changes as small and simple as possible. Avoid large, complex changes.
3. **Check-in**: Before starting, check in with the user to verify the plan.
4. **Iterative Work**: Complete todo items one at a time, marking them as complete as you go. After each step, provide a high-level summary of what was changed.
5. **Documentation**: For each task, create or update a `todo.md` file and update it as you go. Ensure the codebase can always be restored to a stable point.
6. **Review**: At the end of each task, add a review section to `docs/TASKS.md` summarizing the changes and any relevant notes.

## Project Overview

AI.ProfilePhotoMaker is a full-stack application that generates professional profile photos using AI. Users upload selfies to train custom AI models through Replicate.com's FLUX API, then generate styled professional photos.

**Tech Stack:**
- Backend: .NET 8 Web API with Entity Framework Core, ASP.NET Identity, JWT auth
- Frontend: Angular 19 with TypeScript and SASS
- Database: SQL Server
- AI: Replicate.com FLUX.1 models
- Storage: Local filesystem (Azure Blob planned)

## Best Practices

- **Controllers should be thin**: Move business logic to services.
- **Service Layer**: Use dedicated service classes for business operations.
- **Small, focused changes**: Each change should impact as little code as possible.
- **Testing**: All changes must be covered by tests. Run tests after every change.
- **Documentation**: Update or create documentation for every significant change.
- **Error Handling**: Use try-catch with proper logging and return appropriate HTTP status codes.
- **Dependency Injection**: Register all services in `Program.cs` and use constructor injection.

## Documentation Structure

- `README.md` - Main project overview and getting started
- `/docs/ARCHITECTURE.md` - System architecture and design patterns
- `/docs/PROJECT_PLAN.md` - Project milestones and timeline
- `/docs/TASKS.md` - Detailed task list and current status
- `/docs/SETUP.md` - Development environment setup instructions
- `/docs/REFACTOR.md` - Comprehensive refactoring documentation

## Code Organization

- **Component Size Limits**: Keep Angular components under 400 lines. Break up large components.
- **Service Separation**: Business logic in services, not components or controllers.
- **File Organization**: One class/interface per file.
- **Avoid Code Duplication**: Extract common logic into shared services/utilities.

## API Design Principles

- **Consistent Response Format**: All APIs should return `{ success: boolean, data?: any, error?: any }`.
- **Validation**: Use DTOs with validation attributes.
- **Resource Organization**: Group related endpoints under logical controllers.

## Security

- **Authentication**: Use JWT tokens with proper validation.
- **Authorization**: Apply `[Authorize]` attributes to protected endpoints.
- **Input Validation**: Validate all user inputs and API parameters.
- **Secret Management**: Use configuration and user secrets for sensitive data.

## Restore Points

- Always ensure the codebase can be restored to a stable point after each change.
- Use feature branches for major changes and keep PRs small and focused.

---

_This file is inspired by the workflow and structure in `CLAUDE.md` and is intended for Copilot and similar AI coding assistants._

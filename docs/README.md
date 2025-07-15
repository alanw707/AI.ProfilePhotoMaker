# AI Profile Photo Maker Documentation

Welcome to the comprehensive documentation for the AI Profile Photo Maker application. This documentation covers all major features, architecture, and implementation details.

## Quick Navigation

### 🚀 Getting Started
- [Project Plan](./PROJECT_PLAN.md) - Complete project overview and roadmap
- [Architecture Overview](./ARCHITECTURE_OVERVIEW.md) - System architecture and technical design
- [API Reference](./API_REFERENCE.md) - Complete REST API documentation

### 🔧 Core Features
- [Authentication System](./AUTHENTICATION.md) - User authentication and OAuth integration
- [Photo Processing & AI Generation](./PHOTO_PROCESSING.md) - AI model training and image generation
- [Gallery Management](./GALLERY_MANAGEMENT.md) - Image gallery and operations
- [Credit System & Payments](./CREDIT_SYSTEM.md) - Credit management and Stripe integration
- [Style Selection](./STYLE_SELECTION.md) - Photo style customization system

### 📋 Additional Resources
- [OAuth Troubleshooting](./OAUTH_TROUBLESHOOTING.md) - OAuth integration issues
- [Refactoring Guide](./REFACTOR.md) - Code improvement guidelines
- [Task Management](./TASKS.md) - Development task tracking

## System Overview

The AI Profile Photo Maker is a full-stack web application that uses AI to generate professional profile photos from user selfies. Built with .NET 8 and Angular 19, it offers:

- **AI-Powered Generation**: Custom model training using Replicate's FLUX technology
- **23+ Professional Styles**: From LinkedIn to creative artistic styles
- **Credit-Based System**: Flexible pricing with free tier and premium packages
- **Self-Healing Gallery**: Automatic repair of database-filesystem inconsistencies
- **Photo Enhancement**: One-click photo improvement using AI
- **OAuth Integration**: Google, Facebook, and Apple sign-in support

## Architecture Highlights

```
Frontend (Angular 19) ↔ Backend (.NET 8 API) ↔ External APIs
     ↓                       ↓                    ↓
  Local Storage         SQLite/SQL Server     Replicate AI
  Service Worker        File System           Stripe Payments
  PWA Ready            Background Jobs        OAuth Providers
```

## Key Features

### 🤖 AI Integration
- **Custom Model Training**: Train personalized AI models on user selfies
- **Multiple Style Generation**: Generate 2 photos per style across 23+ options
- **Photo Enhancement**: Improve existing photos with AI
- **Webhook Integration**: Real-time updates on training and generation progress

### 💳 Business Model
- **Free Tier**: 3 weekly credits for basic users
- **Credit Packages**: Purchase credits for unlimited generation
- **Stripe Integration**: Secure payment processing with webhook validation
- **Simulation Mode**: Development-friendly payment testing

### 🔒 Security & Performance
- **JWT Authentication**: Stateless authentication with refresh capability
- **OAuth Providers**: Secure third-party authentication
- **Self-Healing Data**: Automatic repair of data inconsistencies
- **Caching Strategy**: Multi-layer caching for optimal performance

## Development Status

### ✅ Completed Features
- Full authentication system with OAuth
- AI model training and image generation
- Gallery management with filtering and pagination
- Credit system with weekly reset
- Photo enhancement feature
- Responsive Angular frontend
- Comprehensive API with Swagger documentation

### 🔄 In Progress
- Payment integration (Stripe webhook testing)
- Background cleanup services
- Production deployment preparation

### 📋 Planned
- Real-time notifications
- Mobile app support
- Advanced analytics dashboard
- Cloud storage migration

## Quick Start Guide

### Prerequisites
- .NET 8.0 SDK
- Node.js 20+
- Angular CLI 19+

### Development Setup
```bash
# Clone repository
git clone [repository-url]

# Backend setup
cd AI.ProfilePhotoMaker.API
dotnet restore
dotnet run

# Frontend setup
cd ../AI.ProfilePhotoMaker.UI
npm install
npm run dev:local
```

### Environment Configuration
- **Development**: Uses SQLite and local file storage
- **Ngrok**: Configured for OAuth callback testing
- **Production**: SQL Server and cloud storage ready

## Documentation Structure

Each feature document includes:
- **Overview**: Feature purpose and capabilities
- **Implementation Details**: Technical architecture and code examples
- **API Reference**: Endpoint documentation with examples
- **Best Practices**: Security, performance, and UX guidelines
- **Troubleshooting**: Common issues and solutions

## Contributing

When updating documentation:
1. Follow the established structure and format
2. Include code examples for complex concepts
3. Update API references when endpoints change
4. Add troubleshooting sections for known issues
5. Keep examples current with latest implementation

## Version History

- **v1.0** (July 2025): Initial comprehensive documentation
- Covers all major features implemented through July 2025
- Includes hybrid filesystem-database architecture
- Documents self-healing gallery system

For detailed feature history, see [Project Plan](./PROJECT_PLAN.md).

---

*Last updated: July 2025*
*For questions or clarifications, refer to the specific feature documentation or API reference.*
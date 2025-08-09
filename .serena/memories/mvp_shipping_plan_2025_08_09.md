# MVP Shipping Plan - AI Profile Photo Maker - August 9, 2025

## Brainstorming Session Summary
**Context**: User wants to ship AI Profile Photo Maker as MVP ASAP, considering stack migration from C# to NextJS but decided to keep C# and ship with full features including free tier.

## Key Decisions Made

### Stack Decision: KEEP C# + Angular
- **Rationale**: Current issues (proxy, static files, webhooks) were configuration problems we already solved, not architectural limitations
- **Architecture Health**: 7.7/10 - solid, production-ready system
- **Migration Cost**: 2-3 months vs immediate deployment
- **Business Impact**: Keep proven system, focus on shipping

### Business Model: Full-Feature MVP with Generous Free Tier
- **Free Tier**: 20 photo uploads, 5 AI generations, 30-day storage
- **Pro Plan**: $9.99/month - 100 uploads, 50 generations, 1-year storage
- **Business Plan**: $29.99/month - 500 uploads, 200 generations, unlimited storage
- **Domain**: aiprofilephotomaker.com

## MVP Shipping Plan (2-3 Days Total)

### Pre-Deployment Requirements
1. **Stripe Account Setup** (30 minutes)
   - Register at dashboard.stripe.com
   - Get API keys (publishable, secret, webhook)
   - Create products and pricing plans

2. **Domain Configuration**
   - api.aiprofilephotomaker.com → Azure App Service
   - app.aiprofilephotomaker.com → Azure Static Web Apps  
   - aiprofilephotomaker.com → Azure Static Web Apps (root)

### Day 1: Backend Infrastructure (6 hours)
1. **Azure SQL Database** (2 hours)
   ```bash
   az sql server create --name "aiprofilemaker-sql" --resource-group "aiprofilemaker-prod"
   az sql db create --name "aiprofilemaker-db" --service-objective "S1"
   ```

2. **Azure App Service** (4 hours)
   ```bash
   az webapp create --name "aiprofilemaker-api" --runtime "DOTNETCORE:8.0"
   # Deploy current working code with full Stripe integration
   ```

### Day 2: Frontend & Domain (4 hours)
1. **Angular Production Build** (2 hours)
   - Update environment.prod.ts with production API URLs
   - Build with: `ng build --configuration production`
   - Deploy to Azure Static Web Apps

2. **Custom Domain + SSL** (2 hours)
   - Configure DNS records
   - Add custom hostnames to Azure services
   - SSL certificates auto-managed by Azure

### Production Configuration
```json
{
  "AppBaseUrl": "https://app.aiprofilephotomaker.com",
  "ExternalApiBaseUrl": "https://api.aiprofilephotomaker.com", 
  "Stripe": {
    "PublishableKey": "[STRIPE_KEY]",
    "SecretKey": "[STRIPE_SECRET]"
  },
  "PricingTiers": {
    "Free": { "PhotoUploads": 20, "AiGenerations": 5, "StorageDays": 30 },
    "Pro": { "Price": 999, "PhotoUploads": 100, "AiGenerations": 50, "StorageDays": 365 }
  }
}
```

### Testing Checklist
- [ ] Complete user registration flow
- [ ] Photo upload and AI generation (free tier)
- [ ] Upgrade prompts and Stripe checkout
- [ ] Payment processing and feature unlocking
- [ ] Download functionality

## Current System Status
- **Core Functionality**: ✅ Working (OAuth, photo upload, AI generation, download)
- **Payment Integration**: ✅ Stripe code ready, needs API keys
- **External APIs**: ✅ Replicate integration working
- **Database**: ✅ Entity Framework migrations ready
- **Storage**: ✅ Azure Blob Storage configured

## MVP Success Metrics
- **Week 1**: 100 free signups, 10 generating photos
- **Week 2**: 5% conversion to paid plans
- **Month 1**: $500 MRR (Monthly Recurring Revenue)
- **Month 2**: $2000 MRR + product-market fit validation

## Launch Strategy
1. **Free Tier**: No credit card required, generous limits
2. **Value Demonstration**: Users see quality before paying
3. **Natural Upgrade**: Clear prompts when hitting limits
4. **Professional Appearance**: Complete business from day 1

## Next Steps (When Ready)
1. Complete Stripe account setup
2. Configure Azure infrastructure 
3. Deploy API with production configuration
4. Deploy frontend with payment UI
5. Configure custom domains
6. End-to-end testing
7. Launch at aiprofilephotomaker.com

## Technical Notes
- Keep dual URL configuration (AppBaseUrl vs ExternalApiBaseUrl)
- Maintain ngrok development setup for local work
- Production uses Azure-managed SSL certificates
- Database migrations ready for production deployment
- All external integrations (Google OAuth, Replicate API) tested and working

## Business Context
- AI Profile Photo Maker: Professional AI headshots via Replicate API
- Target: Professionals needing quality profile photos
- Subscription-based business model with free tier for adoption
- Ready for immediate MVP launch with current C# + Angular stack
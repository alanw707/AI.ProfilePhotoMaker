# 🔍 Automated Code Review Workflows

Comprehensive GitHub Actions workflows for **security**, **performance**, and **quality** analysis of the AI Profile Photo Maker application.

## 📋 Overview

This repository implements a multi-layered automated code review system that:

- 🛡️ **Scans for security vulnerabilities** in both .NET and Angular code
- ⚡ **Monitors performance** of backend APIs and frontend bundles  
- 🔍 **Detects potential bugs** using static analysis and linting
- 📊 **Enforces quality gates** with comprehensive reporting

## 🚀 Workflows

### 1. 🔍 PR Code Review (`pr-code-review.yml`)

**Trigger:** Every pull request to `main` or `develop`

**Jobs:**
- **🛡️ Security Analysis**
  - CodeQL scanning for C# and TypeScript
  - npm audit for known vulnerabilities  
  - Secret detection with TruffleHog
  - Dependency vulnerability analysis

- **🎨 Frontend Quality**  
  - ESLint analysis with error reporting
  - Prettier format validation
  - Unit test coverage (>80% required)
  - Bundle size analysis
  - Lighthouse CI performance metrics

- **⚙️ Backend Quality**
  - Roslyn analyzer validation
  - Unit test coverage (>80% required) 
  - Performance benchmarking
  - Entity Framework migration validation

- **🧪 Integration Testing**
  - Full E2E testing with Playwright
  - Cross-browser compatibility
  - Authentication flow validation

- **📊 Quality Gate**
  - Consolidated pass/fail reporting
  - Blocks merge on critical failures

### 2. ⚡ Performance Monitoring (`performance-monitoring.yml`)

**Trigger:** PRs + weekly schedule

**Backend Monitoring:**
- BenchmarkDotNet performance tests
- Database operation profiling
- API response time analysis
- Memory usage tracking

**Frontend Monitoring:**
- Bundle size tracking (limits: 500KB main, 1MB vendor)
- Core Web Vitals measurement
- Lighthouse performance scoring
- Resource optimization analysis

## 🔧 Configuration Files

### Security & Analysis

```
.github/
├── codeql/
│   └── codeql-config.yml          # CodeQL security rules
└── dependabot.yml                 # Dependency updates
```

### Frontend Quality

```
AI.ProfilePhotoMaker.UI/
├── .lighthouserc.json            # Performance budgets  
├── .audit-ci.json               # Security audit config
└── .bundle-analyzer.json        # Bundle size limits
```

## 🎯 Quality Standards

### Security Requirements
- ✅ **No Critical/High vulnerabilities** in dependencies
- ✅ **No secrets** committed to repository  
- ✅ **CodeQL security rules** must pass
- ⚠️ **Moderate vulnerabilities** trigger warnings

### Performance Requirements  
- ✅ **Frontend bundles** < 500KB (main), < 1MB (vendor)
- ✅ **API responses** < 200ms average
- ✅ **Core Web Vitals** meeting thresholds:
  - LCP < 4s, FCP < 2s, CLS < 0.1
- ✅ **Lighthouse score** > 80/100

### Code Quality Requirements
- ✅ **Test coverage** > 80% (both frontend/backend)
- ✅ **ESLint rules** must pass (no errors)
- ✅ **Prettier formatting** enforced
- ✅ **Roslyn analyzers** must pass (C#)

## 🚀 Setup Instructions

### 1. Repository Secrets

Add these secrets in GitHub repository settings:

```bash
# Required for SonarCloud
SONAR_TOKEN=your_sonar_token

# Optional: Enhanced security scanning  
SNYK_TOKEN=your_snyk_token
```

### 2. Branch Protection

Configure branch protection rules:

```yaml
main:
  required_status_checks:
    - "Security Scan"
    - "Frontend Analysis" 
    - "Backend Analysis"
    - "Quality Gate"
  required_reviews: 1
  dismiss_stale_reviews: true
```

### 3. Local Development

Install pre-commit hooks:

```bash
# Frontend
cd AI.ProfilePhotoMaker.UI
npm run prepare  # Installs Husky hooks

# Backend  
cd AI.ProfilePhotoMaker.API
dotnet tool install --global dotnet-format
```

## 📊 Workflow Reports

### PR Review Summary

Each PR gets a comprehensive summary:

```
🎯 PR Code Review Summary

🛡️ Security Analysis: ✅ PASSED
🎨 Frontend Quality: ✅ PASSED  
⚙️ Backend Quality: ✅ PASSED
🧪 Integration Tests: ✅ PASSED

✅ Quality gate PASSED - All checks successful!
```

### Performance Impact

Performance changes tracked with:

```
📦 Bundle Analysis Results
- Main Bundle: 420KB ✅ 
- Vendor Bundle: 850KB ✅

⚡ Core Web Vitals:
- FCP: 1.2s ✅
- LCP: 2.8s ✅  
- CLS: 0.05 ✅
- Performance Score: 92/100 ✅
```

## 🛠️ Maintenance

### Weekly Tasks

1. **Review Dependabot PRs** - Security updates auto-created
2. **Monitor performance trends** - Weekly automated reports  
3. **Update quality thresholds** - Tighten rules as code improves

### Monthly Tasks

1. **Review SonarCloud metrics** - Technical debt trends
2. **Audit security configurations** - Update CodeQL rules
3. **Performance baseline updates** - Adjust benchmark thresholds

## 🤝 Contributing

### Before Creating PRs

```bash
# Run quality checks locally
cd AI.ProfilePhotoMaker.UI
npm run quality:check

cd ../AI.ProfilePhotoMaker.API  
dotnet build --verbosity normal
dotnet test
```

### Troubleshooting

**Common Issues:**

1. **Bundle size exceeded**
   ```bash
   npm run build:mvp-v1 -- --stats-json
   npx webpack-bundle-analyzer dist/stats.json
   ```

2. **Test coverage too low**
   ```bash
   # Backend
   dotnet test --collect:"XPlat Code Coverage"
   
   # Frontend
   npm test -- --code-coverage
   ```

3. **Security vulnerabilities**
   ```bash
   npm audit fix
   dotnet list package --vulnerable
   ```

## 📈 Metrics & Reporting

The workflows generate comprehensive metrics:

- **Security**: Vulnerability count, risk severity
- **Performance**: Bundle sizes, response times, Core Web Vitals
- **Quality**: Test coverage, code complexity, maintainability
- **Reliability**: Build success rate, test stability

All metrics are tracked over time and available in:
- GitHub Actions run summaries
- SonarCloud dashboard  
- Lighthouse CI reports
- Performance benchmark artifacts

## 🎯 Next Steps

1. **Enable SonarCloud** integration with quality gates
2. **Add performance budgets** to CI blocking
3. **Implement security scanning** in development containers  
4. **Set up monitoring** for production performance metrics
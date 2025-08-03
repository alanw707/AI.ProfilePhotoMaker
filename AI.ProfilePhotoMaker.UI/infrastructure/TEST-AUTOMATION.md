# Test Automation Strategy - Infrastructure & Deployment Validation

## 🎯 Test Automation Objectives

**Primary Goals**:
1. **Validate infrastructure deployment** - Ensure all resources are created correctly
2. **Test deployment pipeline** - Verify end-to-end deployment process
3. **Validate application functionality** - Confirm app works after deployment
4. **Test rollback scenarios** - Ensure disaster recovery works
5. **Monitor regression prevention** - Catch deployment issues early

---

## 🧪 Test Categories & Implementation

### 1. Infrastructure Validation Tests

**Purpose**: Verify Azure resources are created with correct configuration

```yaml
# infrastructure-tests.yml
name: Infrastructure Validation Tests

tests:
  resource_existence:
    - name: "Resource Group exists"
      type: azure_resource_check
      resource: resource_group
      expected: exists
      
    - name: "Managed Identity created"
      type: azure_resource_check
      resource: managed_identity
      expected: exists
      properties:
        - name: "principalId"
          type: guid
          required: true
          
    - name: "Key Vault accessible"
      type: azure_resource_check
      resource: key_vault
      expected: exists
      access_tests:
        - managed_identity_can_read_secrets: true
        
    - name: "Container Registry accessible"
      type: azure_resource_check
      resource: container_registry
      expected: exists
      access_tests:
        - managed_identity_can_pull: true
        
    - name: "SQL Database connectable"
      type: azure_resource_check
      resource: sql_database
      expected: exists
      connectivity_tests:
        - managed_identity_can_connect: true
        
    - name: "Storage Account accessible"
      type: azure_resource_check
      resource: storage_account
      expected: exists
      access_tests:
        - managed_identity_can_write_blobs: true
        
    - name: "Container Apps running"
      type: azure_resource_check
      resource: container_apps
      expected: exists
      health_tests:
        - api_health_check: "200"
        - ui_health_check: "200"
```

### 2. Deployment Pipeline Tests

**Purpose**: Validate the entire deployment process works correctly

```yaml
# deployment-tests.yml
name: Deployment Pipeline Tests

stages:
  pre_deployment:
    - name: "Template validation"
      type: bicep_validation
      template: main.bicep
      expected: valid
      
    - name: "Parameter validation"
      type: parameter_validation
      parameters: staging.parameters.json
      expected: valid
      
    - name: "Cost estimation"
      type: cost_analysis
      template: main.bicep
      max_monthly_cost: 400
      
  deployment:
    - name: "Infrastructure deployment"
      type: bicep_deployment
      template: main.bicep
      mode: incremental
      timeout: 30m
      expected: success
      
    - name: "Container build and push"
      type: container_operations
      images:
        - api: "should build and push successfully"
        - ui: "should build and push successfully"
        
    - name: "Application deployment"
      type: container_app_deployment
      expected: success
      timeout: 15m
      
  post_deployment:
    - name: "Health check validation"
      type: endpoint_testing
      endpoints:
        - api: "/health"
        - ui: "/"
      expected_status: 200
      timeout: 5m
      
    - name: "Database migration validation"
      type: database_testing
      migration_status: completed
      table_existence: required_tables_exist
      
    - name: "Integration testing"
      type: api_testing
      test_suite: postman_collection
      expected: all_pass
```

### 3. Application Functionality Tests

**Purpose**: Validate that the deployed application works correctly

```typescript
// tests/integration/application.test.ts
describe('Application Integration Tests', () => {
  const apiUrl = process.env.API_URL;
  const uiUrl = process.env.UI_URL;

  beforeAll(async () => {
    // Wait for application to be ready
    await waitForHealthcheck(apiUrl + '/health', 300000); // 5 min timeout
  });

  describe('API Functionality', () => {
    test('Health endpoint responds', async () => {
      const response = await fetch(`${apiUrl}/health`);
      expect(response.status).toBe(200);
    });

    test('Authentication endpoints work', async () => {
      const response = await fetch(`${apiUrl}/api/auth/test`);
      expect(response.status).toBe(200);
    });

    test('Database connection works', async () => {
      const response = await fetch(`${apiUrl}/api/health/database`);
      expect(response.status).toBe(200);
      const data = await response.json();
      expect(data.database).toBe('connected');
    });

    test('Blob storage connection works', async () => {
      const response = await fetch(`${apiUrl}/api/health/storage`);
      expect(response.status).toBe(200);
      const data = await response.json();
      expect(data.storage).toBe('connected');
    });

    test('External API connections work', async () => {
      const response = await fetch(`${apiUrl}/api/health/external`);
      expect(response.status).toBe(200);
      const data = await response.json();
      expect(data.replicate).toBe('connected');
    });
  });

  describe('UI Functionality', () => {
    test('Frontend loads successfully', async () => {
      const response = await fetch(uiUrl);
      expect(response.status).toBe(200);
    });

    test('Frontend can reach API', async () => {
      // Test via browser automation
      const browser = await puppeteer.launch();
      const page = await browser.newPage();
      await page.goto(uiUrl);
      
      // Check if API calls work
      const apiCall = await page.evaluate(async () => {
        const response = await fetch('/api/health');
        return response.status;
      });
      
      expect(apiCall).toBe(200);
      await browser.close();
    });
  });

  describe('Database Migration Validation', () => {
    test('Required tables exist', async () => {
      const response = await fetch(`${apiUrl}/api/database/tables`);
      expect(response.status).toBe(200);
      const data = await response.json();
      
      const requiredTables = [
        'AspNetUsers',
        'StylePresets',
        'GeneratedImages',
        'CreditPackages'
      ];
      
      requiredTables.forEach(table => {
        expect(data.tables).toContain(table);
      });
    });

    test('Database migrations completed', async () => {
      const response = await fetch(`${apiUrl}/api/database/migrations`);
      expect(response.status).toBe(200);
      const data = await response.json();
      expect(data.status).toBe('completed');
    });
  });
});
```

### 4. Security Validation Tests

**Purpose**: Ensure security configurations are correctly implemented

```yaml
# security-tests.yml
name: Security Validation Tests

tests:
  managed_identity:
    - name: "Managed Identity can access Key Vault"
      type: access_test
      resource: key_vault
      identity: managed_identity
      expected: success
      
    - name: "Managed Identity can access SQL Database"
      type: access_test
      resource: sql_database
      identity: managed_identity
      expected: success
      
    - name: "Managed Identity cannot access unauthorized resources"
      type: negative_access_test
      resource: other_key_vaults
      identity: managed_identity
      expected: denied
      
  secrets_management:
    - name: "No secrets in environment variables"
      type: environment_scan
      containers: [api, ui]
      scan_for: [passwords, keys, tokens]
      expected: none_found
      
    - name: "All secrets retrieved from Key Vault"
      type: secret_source_validation
      expected_source: key_vault
      
  network_security:
    - name: "No public access to databases"
      type: network_test
      resource: sql_database
      public_access: false
      expected: denied
      
    - name: "Container Apps accessible from internet"
      type: network_test
      resource: container_apps
      public_access: true
      expected: allowed
```

### 5. Performance & Scalability Tests

**Purpose**: Validate application performance under load

```javascript
// tests/performance/load-test.js
import http from 'k6/http';
import { check, sleep } from 'k6';

export let options = {
  stages: [
    { duration: '2m', target: 10 }, // Ramp up
    { duration: '5m', target: 50 }, // Stay at 50 users
    { duration: '2m', target: 0 },  // Ramp down
  ],
  thresholds: {
    http_req_duration: ['p(95)<2000'], // 95% of requests under 2s
    http_req_failed: ['rate<0.05'],    // Error rate under 5%
  },
};

export default function () {
  const apiUrl = __ENV.API_URL;
  
  // Test API endpoints
  let response = http.get(`${apiUrl}/api/health`);
  check(response, {
    'API health check status is 200': (r) => r.status === 200,
    'API response time < 500ms': (r) => r.timings.duration < 500,
  });
  
  // Test UI loading
  response = http.get(__ENV.UI_URL);
  check(response, {
    'UI loads successfully': (r) => r.status === 200,
    'UI response time < 1000ms': (r) => r.timings.duration < 1000,
  });
  
  sleep(1);
}
```

### 6. Rollback & Disaster Recovery Tests

**Purpose**: Validate that rollback procedures work correctly

```yaml
# rollback-tests.yml
name: Rollback & Disaster Recovery Tests

scenarios:
  deployment_rollback:
    - name: "Deploy faulty version"
      type: deployment
      version: faulty
      expected: deployed
      
    - name: "Trigger automatic rollback"
      type: health_check_failure
      duration: 5m
      expected: rollback_triggered
      
    - name: "Validate rollback completed"
      type: deployment_check
      version: previous
      expected: deployed
      
  database_recovery:
    - name: "Create test data"
      type: database_operation
      operation: insert_test_data
      
    - name: "Simulate database corruption"
      type: database_operation
      operation: simulate_corruption
      
    - name: "Trigger database recovery"
      type: recovery_operation
      method: point_in_time_restore
      
    - name: "Validate data integrity"
      type: database_validation
      expected: test_data_exists
      
  infrastructure_recovery:
    - name: "Simulate resource deletion"
      type: infrastructure_operation
      operation: delete_container_app
      
    - name: "Trigger infrastructure recovery"
      type: bicep_deployment
      mode: incremental
      
    - name: "Validate resource recreation"
      type: resource_validation
      expected: all_resources_exist
```

---

## 🚀 Test Automation Implementation

### PowerShell Test Runner

```powershell
# tests/scripts/run-infrastructure-tests.ps1
param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory=$true)]
    [string]$Environment
)

Write-Host "🧪 Running Infrastructure Tests for $Environment" -ForegroundColor Green

# Test 1: Resource Existence
Write-Host "Testing resource existence..." -ForegroundColor Yellow
$resources = az resource list --resource-group $ResourceGroupName --output json | ConvertFrom-Json

$requiredResources = @(
    "Microsoft.ManagedIdentity/userAssignedIdentities",
    "Microsoft.KeyVault/vaults",
    "Microsoft.ContainerRegistry/registries",
    "Microsoft.Sql/servers",
    "Microsoft.Storage/storageAccounts",
    "Microsoft.App/managedEnvironments",
    "Microsoft.App/containerApps"
)

foreach ($resourceType in $requiredResources) {
    $resource = $resources | Where-Object { $_.type -eq $resourceType }
    if ($resource) {
        Write-Host "✅ $resourceType exists" -ForegroundColor Green
    } else {
        Write-Host "❌ $resourceType missing" -ForegroundColor Red
        exit 1
    }
}

# Test 2: Connectivity Tests
Write-Host "Testing connectivity..." -ForegroundColor Yellow

# Test API health
$apiUrl = az containerapp show --name "ca-aiprofilemaker-api-$Environment" --resource-group $ResourceGroupName --query "properties.configuration.ingress.fqdn" -o tsv
$healthResponse = Invoke-RestMethod -Uri "https://$apiUrl/health" -Method Get -TimeoutSec 30

if ($healthResponse.status -eq "Healthy") {
    Write-Host "✅ API health check passed" -ForegroundColor Green
} else {
    Write-Host "❌ API health check failed" -ForegroundColor Red
    exit 1
}

# Test UI availability
$uiUrl = az containerapp show --name "ca-aiprofilemaker-ui-$Environment" --resource-group $ResourceGroupName --query "properties.configuration.ingress.fqdn" -o tsv
$uiResponse = Invoke-WebRequest -Uri "https://$uiUrl" -Method Get -TimeoutSec 30

if ($uiResponse.StatusCode -eq 200) {
    Write-Host "✅ UI availability check passed" -ForegroundColor Green
} else {
    Write-Host "❌ UI availability check failed" -ForegroundColor Red
    exit 1
}

Write-Host "🎉 All infrastructure tests passed!" -ForegroundColor Green
```

### GitHub Actions Integration

```yaml
# .github/workflows/test-automation.yml
name: 🧪 Test Automation

on:
  deployment_status:
  schedule:
    - cron: '0 */6 * * *' # Every 6 hours
  workflow_dispatch:

jobs:
  infrastructure-tests:
    name: 🏗️ Infrastructure Tests
    runs-on: ubuntu-latest
    if: github.event.deployment_status.state == 'success'
    
    steps:
      - name: 📥 Checkout
        uses: actions/checkout@v4
        
      - name: 🔐 Azure Login
        uses: azure/login@v1
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
          
      - name: 🧪 Run Infrastructure Tests
        shell: pwsh
        run: |
          ./infrastructure/tests/scripts/run-infrastructure-tests.ps1 -ResourceGroupName "rg-aiprofilemaker-staging" -Environment "staging"
          
  application-tests:
    name: 🚀 Application Tests
    runs-on: ubuntu-latest
    needs: infrastructure-tests
    
    steps:
      - name: 📥 Checkout
        uses: actions/checkout@v4
        
      - name: 🏗️ Setup Node.js
        uses: actions/setup-node@v4
        with:
          node-version: '18'
          
      - name: 📦 Install Dependencies
        run: |
          cd infrastructure/tests
          npm install
          
      - name: 🧪 Run Integration Tests
        run: |
          cd infrastructure/tests
          npm run test:integration
        env:
          API_URL: ${{ needs.infrastructure-tests.outputs.api-url }}
          UI_URL: ${{ needs.infrastructure-tests.outputs.ui-url }}
          
  performance-tests:
    name: ⚡ Performance Tests
    runs-on: ubuntu-latest
    needs: application-tests
    
    steps:
      - name: 📥 Checkout
        uses: actions/checkout@v4
        
      - name: 🏗️ Setup k6
        run: |
          sudo apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys C5AD17C747E3415A3642D57D77C6C491D6AC1D69
          echo "deb https://dl.k6.io/deb stable main" | sudo tee /etc/apt/sources.list.d/k6.list
          sudo apt-get update
          sudo apt-get install k6
          
      - name: ⚡ Run Performance Tests
        run: |
          k6 run infrastructure/tests/performance/load-test.js
        env:
          API_URL: ${{ needs.infrastructure-tests.outputs.api-url }}
          UI_URL: ${{ needs.infrastructure-tests.outputs.ui-url }}
          
  security-tests:
    name: 🛡️ Security Tests
    runs-on: ubuntu-latest
    needs: infrastructure-tests
    
    steps:
      - name: 📥 Checkout
        uses: actions/checkout@v4
        
      - name: 🔐 Azure Login
        uses: azure/login@v1
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
          
      - name: 🛡️ Run Security Tests
        shell: pwsh
        run: |
          ./infrastructure/tests/scripts/run-security-tests.ps1 -ResourceGroupName "rg-aiprofilemaker-staging" -Environment "staging"
```

---

## 📊 Test Reporting & Monitoring

### Test Results Dashboard

```json
{
  "test_results": {
    "infrastructure": {
      "status": "passed",
      "tests_run": 15,
      "tests_passed": 15,
      "duration": "5m 32s"
    },
    "application": {
      "status": "passed", 
      "tests_run": 25,
      "tests_passed": 25,
      "duration": "8m 45s"
    },
    "performance": {
      "status": "passed",
      "avg_response_time": "342ms",
      "error_rate": "0.2%",
      "duration": "12m 15s"
    },
    "security": {
      "status": "passed",
      "vulnerabilities": 0,
      "compliance_score": "95%",
      "duration": "6m 20s"
    }
  },
  "deployment_health": "healthy",
  "last_tested": "2024-08-03T10:30:00Z"
}
```

### Automated Alerts

```yaml
# Alert conditions
alerts:
  infrastructure_test_failure:
    condition: infrastructure.status == "failed"
    notification: slack_ops_channel
    escalation: 15m
    
  performance_degradation:
    condition: performance.avg_response_time > "2000ms"
    notification: slack_dev_channel
    escalation: 5m
    
  security_vulnerability:
    condition: security.vulnerabilities > 0
    notification: security_team_email
    escalation: immediate
```

---

## ✅ Success Criteria

**Infrastructure Tests**: All resources exist and are properly configured
**Deployment Tests**: Full pipeline completes without manual intervention  
**Application Tests**: All endpoints respond correctly and database migrations complete
**Security Tests**: Managed identity authentication works, no secrets exposed
**Performance Tests**: Response times <2s, error rate <5%
**Rollback Tests**: Automatic rollback triggers and completes successfully

This comprehensive test automation strategy ensures that all deployment issues are caught early and validates that the infrastructure improvements actually solve the original problems.
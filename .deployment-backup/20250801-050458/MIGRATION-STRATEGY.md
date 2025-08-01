# 🔄 Azure Resource Migration Strategy

## Current State Analysis

**Existing Resources (aiapp* prefix):**
- `aiapp-ai-staging` - Application Insights
- `aiapp-asp-staging` - App Service Plan
- `aiapp-la-staging` - Log Analytics Workspace
- `aiapp-sql-staging-*` - SQL Server
- `aiapp-swa-staging` - Static Web App
- `aiappapi-staging` - App Service
- `aiappdb` - SQL Database
- `aiappkvstaf544mjgkzp` - Key Vault
- `aiappstf544mjgkzp` - Storage Account

**Target Resources (aiprofilephotomaker* prefix):**
- `aiprofilephotomaker-ai-staging` - Application Insights
- `aiprofilephotomaker-asp-staging` - App Service Plan
- `aiprofilephotomaker-la-staging` - Log Analytics Workspace
- `aiprofilephotomaker-sql-staging` - SQL Server
- `aiprofilephotomaker-swa-staging` - Static Web App
- `aiprofilephotomaker-api-staging` - App Service
- `aiprofilephotomaker-db-staging` - SQL Database
- `aiprofilephotomaker-kv-staging` - Key Vault
- `aiprofilephotomaker-st-staging` - Storage Account

## Migration Options

### Option 1: Incremental Deployment (Recommended)
Deploy new resources with correct naming, migrate data, then remove old resources.

**Pros:**
- Zero downtime
- Safe rollback
- Data preservation
- Testing opportunity

**Cons:**
- Temporary cost increase
- Manual data migration steps

### Option 2: Direct Resource Rename
Use Azure CLI/PowerShell to rename existing resources.

**Pros:**
- No data migration
- Cost efficient

**Cons:**  
- Limited rename support in Azure
- Potential downtime
- Higher risk

## Recommended Migration Plan

### Phase 1: Deploy New Infrastructure
```bash
# Deploy new resources with correct naming
az deployment group create \
  --resource-group "ai-profile-photo-maker-staging" \
  --template-file "infrastructure/main.bicep" \
  --parameters "@infrastructure/parameters.staging.json" \
  --mode Incremental
```

### Phase 2: Data Migration
1. **Database Migration**
   ```bash
   # Export from old database
   az sql db export \
     --server aiapp-sql-staging-* \
     --name aiappdb \
     --storage-uri "https://storage.blob.core.windows.net/backups/migration.bacpac"
   
   # Import to new database
   az sql db import \
     --server aiprofilephotomaker-sql-staging \
     --name aiprofilephotomaker-db-staging \
     --storage-uri "https://storage.blob.core.windows.net/backups/migration.bacpac"
   ```

2. **Storage Account Migration**
   ```bash
   # Copy blobs using AzCopy
   azcopy copy \
     "https://aiappstf544mjgkzp.blob.core.windows.net/*" \
     "https://aiprofilephotomaker-st-staging.blob.core.windows.net/" \
     --recursive
   ```

3. **Key Vault Migration**
   ```bash
   # Export and import secrets
   az keyvault secret list --vault-name aiappkvstaf544mjgkzp --query "[].name" -o tsv | \
   while read secret; do
     value=$(az keyvault secret show --vault-name aiappkvstaf544mjgkzp --name $secret --query "value" -o tsv)
     az keyvault secret set --vault-name aiprofilephotomaker-kv-staging --name $secret --value "$value"
   done
   ```

### Phase 3: Application Configuration Update
Update connection strings and configuration to point to new resources.

### Phase 4: Testing & Validation
1. Deploy application to new infrastructure
2. Run full test suite
3. Validate all functionality
4. Performance testing

### Phase 5: Cleanup Old Resources
```bash
# List old resources for deletion
az resource list --resource-group "ai-profile-photo-maker-staging" \
  --query "[?starts_with(name, 'aiapp')].{Name:name, Type:type}" \
  --output table

# Delete old resources (after validation)
az resource delete --ids $(az resource list --resource-group "ai-profile-photo-maker-staging" \
  --query "[?starts_with(name, 'aiapp')].id" -o tsv)
```

## Implementation Timeline

### Week 1: Preparation
- [x] Infrastructure templates updated
- [ ] Migration scripts prepared
- [ ] Backup strategy confirmed

### Week 2: Staging Migration
- [ ] Deploy new staging infrastructure
- [ ] Migrate staging data
- [ ] Update staging application configuration
- [ ] Validate staging deployment

### Week 3: Production Planning
- [ ] Production infrastructure deployment
- [ ] Production migration scheduling
- [ ] Rollback procedures tested

## Risk Mitigation

### Data Loss Prevention
- Full database backup before migration
- Storage account backup/sync
- Key Vault secret export
- Infrastructure templates versioned

### Rollback Strategy
- Keep old resources during validation period
- DNS/traffic routing for quick switchback  
- Automated rollback scripts prepared
- Monitoring alerts configured

## Success Criteria

### Technical Validation
- [ ] All resources use `aiprofilephotomaker-*` naming
- [ ] Application functionality 100% operational
- [ ] Data integrity verified
- [ ] Performance metrics within acceptable range

### Operational Validation
- [ ] Monitoring and alerting functional
- [ ] Backup/restore procedures tested
- [ ] Security configurations verified
- [ ] Cost optimization achieved

## Next Steps

1. **Execute staging migration** with new naming convention
2. **Validate functionality** thoroughly
3. **Update CI/CD pipelines** for new resource names
4. **Plan production migration** with lessons learned
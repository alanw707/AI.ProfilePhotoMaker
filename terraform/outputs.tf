# AI Profile Photo Maker - Terraform Outputs

# Application URLs
output "frontend_url" {
  description = "Frontend application URL"
  value       = "https://${azurerm_container_app.frontend.latest_revision_fqdn}"
}

output "backend_url" {
  description = "Backend API URL"
  value       = "https://${azurerm_container_app.backend.latest_revision_fqdn}"
}

# Infrastructure details
output "container_registry_name" {
  description = "Container Registry name"
  value       = azurerm_container_registry.main.name
}

output "container_registry_login_server" {
  description = "Container Registry login server"
  value       = azurerm_container_registry.main.login_server
}

output "storage_account_name" {
  description = "Storage Account name"
  value       = azurerm_storage_account.main.name
}

output "storage_account_primary_endpoint" {
  description = "Storage Account primary blob endpoint"
  value       = azurerm_storage_account.main.primary_blob_endpoint
}

output "key_vault_name" {
  description = "Key Vault name"
  value       = azurerm_key_vault.main.name
}

output "key_vault_uri" {
  description = "Key Vault URI"
  value       = azurerm_key_vault.main.vault_uri
}

output "sql_server_name" {
  description = "SQL Server name"
  value       = azurerm_mssql_server.main.name
}

output "sql_server_fqdn" {
  description = "SQL Server fully qualified domain name"
  value       = azurerm_mssql_server.main.fully_qualified_domain_name
}

output "sql_database_name" {
  description = "SQL Database name"
  value       = azurerm_mssql_database.main.name
}

output "application_insights_instrumentation_key" {
  description = "Application Insights instrumentation key"
  value       = azurerm_application_insights.main.instrumentation_key
  sensitive   = true
}

output "application_insights_connection_string" {
  description = "Application Insights connection string"
  value       = azurerm_application_insights.main.connection_string
  sensitive   = true
}

# Resource naming outputs (for cleanup scripts)
output "resource_naming_pattern" {
  description = "Resource naming pattern for identification"
  value = {
    unique_suffix     = local.unique_suffix
    container_registry = local.naming.container_registry
    storage_account   = local.naming.storage_account
    key_vault        = local.naming.key_vault
    sql_server       = local.naming.sql_server
    container_env    = local.naming.container_env
    backend_app      = local.naming.backend_app
    frontend_app     = local.naming.frontend_app
  }
}

# Cost optimization insights
output "cost_optimization_summary" {
  description = "Cost optimization configuration summary"
  value = {
    sql_sku                    = var.sql_sku
    container_registry_sku     = var.container_registry_sku
    storage_replication_type   = var.storage_replication_type
    backend_scaling           = "${var.backend_min_replicas}-${var.backend_max_replicas}"
    frontend_scaling          = "${var.frontend_min_replicas}-${var.frontend_max_replicas}"
    log_retention_days        = var.log_analytics_retention_days
    cost_optimization_enabled = var.enable_cost_optimization
  }
}

# Security and compliance outputs
output "security_summary" {
  description = "Security configuration summary"
  value = {
    key_vault_rbac_enabled    = azurerm_key_vault.main.enable_rbac_authorization
    sql_minimum_tls_version   = azurerm_mssql_server.main.minimum_tls_version
    storage_min_tls_version   = azurerm_storage_account.main.min_tls_version
    container_apps_identity   = "SystemAssigned"
    secrets_stored_in_kv      = true
  }
}

# Resource IDs for advanced usage
output "resource_ids" {
  description = "Resource IDs for advanced scenarios"
  value = {
    resource_group_id           = data.azurerm_resource_group.main.id
    container_registry_id       = azurerm_container_registry.main.id
    storage_account_id          = azurerm_storage_account.main.id
    key_vault_id               = azurerm_key_vault.main.id
    sql_server_id              = azurerm_mssql_server.main.id
    sql_database_id            = azurerm_mssql_database.main.id
    container_environment_id    = azurerm_container_app_environment.main.id
    backend_app_id             = azurerm_container_app.backend.id
    frontend_app_id            = azurerm_container_app.frontend.id
    application_insights_id     = azurerm_application_insights.main.id
    log_analytics_workspace_id  = azurerm_log_analytics_workspace.main.id
  }
}
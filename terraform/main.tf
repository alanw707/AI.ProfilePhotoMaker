# AI Profile Photo Maker - Main Terraform Configuration
# Deterministic, idempotent infrastructure deployment

terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~>3.0"
    }
  }
  required_version = ">= 1.0"
}

provider "azurerm" {
  features {
    resource_group {
      prevent_deletion_if_contains_resources = false
    }
    key_vault {
      purge_soft_delete_on_destroy = true
    }
  }
}

# Local values for consistent naming
locals {
  app_name    = var.app_name
  environment = var.environment
  location    = var.location
  
  # Deterministic suffix - consistent across deployments
  # Using a hash of the resource group for predictable uniqueness
  unique_suffix = substr(sha256("${var.resource_group_name}-${var.app_name}"), 0, 8)
  
  # Resource naming convention
  naming = {
    container_registry = "${local.app_name}cr${local.unique_suffix}"
    storage_account   = "${local.app_name}st${local.unique_suffix}"
    key_vault        = "${local.app_name}kv${local.unique_suffix}"
    sql_server       = "${local.app_name}-sql-${local.unique_suffix}"
    sql_database     = "${local.app_name}db"
    container_env    = "${local.app_name}-env-${local.environment}"
    backend_app      = "${local.app_name}-api-${local.environment}"
    frontend_app     = "${local.app_name}-web-${local.environment}"
    app_insights     = "${local.app_name}-ai-${local.environment}"
  }
  
  # Common tags
  common_tags = {
    Environment   = var.environment
    Application   = var.app_name
    ManagedBy     = "Terraform"
    CostCenter    = "Development"
    Project       = "AIProfileMaker"
    CreatedDate   = formatdate("YYYY-MM-DD", timestamp())
  }
}

# Data sources
data "azurerm_client_config" "current" {}

data "azurerm_resource_group" "main" {
  name = var.resource_group_name
}

# Container Registry
resource "azurerm_container_registry" "main" {
  name                = local.naming.container_registry
  resource_group_name = data.azurerm_resource_group.main.name
  location            = local.location
  sku                 = "Basic"
  admin_enabled       = true
  
  tags = local.common_tags
}

# Storage Account
resource "azurerm_storage_account" "main" {
  name                     = local.naming.storage_account
  resource_group_name      = data.azurerm_resource_group.main.name
  location                 = local.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
  min_tls_version          = "TLS1_2"
  
  blob_properties {
    change_feed_enabled = false
    versioning_enabled  = false
  }
  
  tags = local.common_tags
}

resource "azurerm_storage_container" "profile_images" {
  name                  = "profile-images"
  storage_account_name  = azurerm_storage_account.main.name
  container_access_type = "blob"
}

# Application Insights
resource "azurerm_application_insights" "main" {
  name                = local.naming.app_insights
  location            = local.location
  resource_group_name = data.azurerm_resource_group.main.name
  application_type    = "web"
  
  tags = local.common_tags
}

# Log Analytics Workspace
resource "azurerm_log_analytics_workspace" "main" {
  name                = "${local.app_name}-logs-${local.unique_suffix}"
  location            = local.location
  resource_group_name = data.azurerm_resource_group.main.name
  sku                 = "PerGB2018"
  retention_in_days   = 30
  
  tags = local.common_tags
}

# Key Vault
data "azurerm_client_config" "current_client" {}

resource "azurerm_key_vault" "main" {
  name                = local.naming.key_vault
  location            = local.location
  resource_group_name = data.azurerm_resource_group.main.name
  tenant_id           = data.azurerm_client_config.current.tenant_id
  sku_name            = "standard"
  
  enable_rbac_authorization = true
  soft_delete_retention_days = 7
  purge_protection_enabled   = false
  
  tags = local.common_tags
}

# SQL Server
resource "azurerm_mssql_server" "main" {
  name                         = local.naming.sql_server
  resource_group_name          = data.azurerm_resource_group.main.name
  location                     = local.location
  version                      = "12.0"
  administrator_login          = "sqladmin"
  administrator_login_password = var.sql_admin_password
  minimum_tls_version          = "1.2"
  
  tags = local.common_tags
}

resource "azurerm_mssql_database" "main" {
  name      = local.naming.sql_database
  server_id = azurerm_mssql_server.main.id
  sku_name  = "Basic"
  max_size_gb = 2
  
  tags = local.common_tags
}

resource "azurerm_mssql_firewall_rule" "azure_services" {
  name             = "AllowAzureServices"
  server_id        = azurerm_mssql_server.main.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

# Key Vault Secrets
resource "azurerm_key_vault_secret" "jwt_secret" {
  name         = "JwtSecret"
  value        = var.jwt_secret
  key_vault_id = azurerm_key_vault.main.id
  
  depends_on = [azurerm_role_assignment.current_user_kv_admin]
}

resource "azurerm_key_vault_secret" "replicate_token" {
  name         = "ReplicateApiToken"
  value        = var.replicate_api_token
  key_vault_id = azurerm_key_vault.main.id
  
  depends_on = [azurerm_role_assignment.current_user_kv_admin]
}

resource "azurerm_key_vault_secret" "connection_string" {
  name         = "ConnectionString"
  value        = "Server=tcp:${azurerm_mssql_server.main.fully_qualified_domain_name},1433;Initial Catalog=${azurerm_mssql_database.main.name};Authentication=Active Directory Default;Encrypt=True;"
  key_vault_id = azurerm_key_vault.main.id
  
  depends_on = [azurerm_role_assignment.current_user_kv_admin]
}

# Key Vault RBAC
resource "azurerm_role_assignment" "current_user_kv_admin" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Administrator"
  principal_id         = data.azurerm_client_config.current.object_id
}

# Container Apps Environment
resource "azurerm_container_app_environment" "main" {
  name                       = local.naming.container_env
  location                   = local.location
  resource_group_name        = data.azurerm_resource_group.main.name
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id
  
  tags = local.common_tags
}

# Backend Container App
resource "azurerm_container_app" "backend" {
  name                         = local.naming.backend_app
  container_app_environment_id = azurerm_container_app_environment.main.id
  resource_group_name          = data.azurerm_resource_group.main.name
  revision_mode                = "Single"
  
  identity {
    type = "SystemAssigned"
  }
  
  ingress {
    external_enabled = true
    target_port      = 80
    
    traffic_weight {
      percentage      = 100
      latest_revision = true
    }
  }
  
  template {
    min_replicas = 0
    max_replicas = 3
    
    container {
      name   = "api"
      image  = "${azurerm_container_registry.main.login_server}/aiprofilemaker-backend:latest"
      cpu    = 0.5
      memory = "1Gi"
      
      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = title(var.environment)
      }
      
      env {
        name        = "ConnectionStrings__DefaultConnection"
        secret_name = "connection-string"
      }
      
      env {
        name        = "Jwt__Secret"
        secret_name = "jwt-secret"
      }
      
      env {
        name        = "Replicate__ApiToken"
        secret_name = "replicate-token"
      }
      
      env {
        name  = "ApplicationInsights__ConnectionString"
        value = azurerm_application_insights.main.connection_string
      }
    }
  }
  
  secret {
    name  = "connection-string"
    value = "Server=tcp:${azurerm_mssql_server.main.fully_qualified_domain_name},1433;Initial Catalog=${azurerm_mssql_database.main.name};Authentication=Active Directory Default;Encrypt=True;"
  }
  
  secret {
    name  = "jwt-secret"
    value = var.jwt_secret
  }
  
  secret {
    name  = "replicate-token"
    value = var.replicate_api_token
  }
  
  tags = local.common_tags
}

# Frontend Container App
resource "azurerm_container_app" "frontend" {
  name                         = local.naming.frontend_app
  container_app_environment_id = azurerm_container_app_environment.main.id
  resource_group_name          = data.azurerm_resource_group.main.name
  revision_mode                = "Single"
  
  ingress {
    external_enabled = true
    target_port      = 80
    
    traffic_weight {
      percentage      = 100
      latest_revision = true
    }
  }
  
  template {
    min_replicas = 0
    max_replicas = 2
    
    container {
      name   = "web"
      image  = "${azurerm_container_registry.main.login_server}/aiprofilemaker-frontend:latest"
      cpu    = 0.25
      memory = "0.5Gi"
      
      env {
        name  = "API_URL"
        value = "https://${azurerm_container_app.backend.latest_revision_fqdn}"
      }
    }
  }
  
  tags = local.common_tags
}

# Backend Key Vault Access
resource "azurerm_role_assignment" "backend_kv_access" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_container_app.backend.identity[0].principal_id
}
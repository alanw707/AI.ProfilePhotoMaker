# AI Profile Photo Maker - Terraform Variables

variable "app_name" {
  description = "Application name for resource naming"
  type        = string
  default     = "aiprofilemaker"
  
  validation {
    condition     = can(regex("^[a-z0-9]+$", var.app_name))
    error_message = "App name must contain only lowercase letters and numbers."
  }
}

variable "environment" {
  description = "Environment name (staging, production)"
  type        = string
  default     = "staging"
  
  validation {
    condition     = contains(["staging", "production"], var.environment)
    error_message = "Environment must be either 'staging' or 'production'."
  }
}

variable "location" {
  description = "Azure region for resources"
  type        = string
  default     = "East US 2"
  
  validation {
    condition     = contains(["East US", "East US 2", "West US", "West US 2", "Central US"], var.location)
    error_message = "Location must be a valid Azure region."
  }
}

variable "resource_group_name" {
  description = "Name of the resource group"
  type        = string
  default     = "rg-aiprofilemaker-staging"
}

variable "sql_admin_password" {
  description = "SQL Server administrator password"
  type        = string
  sensitive   = true
  
  validation {
    condition     = can(regex("^.{8,128}$", var.sql_admin_password))
    error_message = "SQL admin password must be between 8 and 128 characters."
  }
}

variable "jwt_secret" {
  description = "JWT signing secret"
  type        = string
  sensitive   = true
  
  validation {
    condition     = length(var.jwt_secret) >= 32
    error_message = "JWT secret must be at least 32 characters long."
  }
}

variable "replicate_api_token" {
  description = "Replicate API token for AI services"
  type        = string
  sensitive   = true
  
  validation {
    condition     = can(regex("^r8_[A-Za-z0-9]+$", var.replicate_api_token))
    error_message = "Replicate API token must start with 'r8_' followed by alphanumeric characters."
  }
}

# Cost optimization variables
variable "enable_cost_optimization" {
  description = "Enable cost optimization features"
  type        = bool
  default     = true
}

variable "sql_sku" {
  description = "SQL Database SKU"
  type        = string
  default     = "Basic"
  
  validation {
    condition     = contains(["Basic", "S0", "S1", "P1"], var.sql_sku)
    error_message = "SQL SKU must be one of: Basic, S0, S1, P1."
  }
}

variable "container_registry_sku" {
  description = "Container Registry SKU"
  type        = string
  default     = "Basic"
  
  validation {
    condition     = contains(["Basic", "Standard", "Premium"], var.container_registry_sku)
    error_message = "Container Registry SKU must be one of: Basic, Standard, Premium."
  }
}

variable "storage_replication_type" {
  description = "Storage account replication type"
  type        = string
  default     = "LRS"
  
  validation {
    condition     = contains(["LRS", "GRS", "ZRS", "GZRS"], var.storage_replication_type)
    error_message = "Storage replication type must be one of: LRS, GRS, ZRS, GZRS."
  }
}

# Scaling configuration
variable "backend_min_replicas" {
  description = "Minimum replicas for backend container app"
  type        = number
  default     = 0
  
  validation {
    condition     = var.backend_min_replicas >= 0 && var.backend_min_replicas <= 10
    error_message = "Backend min replicas must be between 0 and 10."
  }
}

variable "backend_max_replicas" {
  description = "Maximum replicas for backend container app"
  type        = number
  default     = 3
  
  validation {
    condition     = var.backend_max_replicas >= 1 && var.backend_max_replicas <= 100
    error_message = "Backend max replicas must be between 1 and 100."
  }
}

variable "frontend_min_replicas" {
  description = "Minimum replicas for frontend container app"
  type        = number
  default     = 0
  
  validation {
    condition     = var.frontend_min_replicas >= 0 && var.frontend_min_replicas <= 10
    error_message = "Frontend min replicas must be between 0 and 10."
  }
}

variable "frontend_max_replicas" {
  description = "Maximum replicas for frontend container app"
  type        = number
  default     = 2
  
  validation {
    condition     = var.frontend_max_replicas >= 1 && var.frontend_max_replicas <= 100
    error_message = "Frontend max replicas must be between 1 and 100."
  }
}

# Monitoring and logging
variable "log_analytics_retention_days" {
  description = "Log Analytics workspace retention in days"
  type        = number
  default     = 30
  
  validation {
    condition     = var.log_analytics_retention_days >= 30 && var.log_analytics_retention_days <= 730
    error_message = "Log Analytics retention must be between 30 and 730 days."
  }
}

variable "enable_application_insights" {
  description = "Enable Application Insights monitoring"
  type        = bool
  default     = true
}
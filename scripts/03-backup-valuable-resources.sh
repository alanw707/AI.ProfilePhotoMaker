#!/bin/bash

# Azure V1 Valuable Resources Backup
# Backs up critical data before selective cleanup

set -e

echo "📦 Azure V1 Valuable Resources Backup"
echo "====================================="
echo ""

# Configuration
V1_RG="aiprofilemaker-v1"
BACKUP_DIR="azure-cleanup-backup/$(date +%Y%m%d-%H%M%S)-v1-backup"

# Create backup directory
mkdir -p "$BACKUP_DIR"
mkdir -p "$BACKUP_DIR/container-images"
mkdir -p "$BACKUP_DIR/database"
mkdir -p "$BACKUP_DIR/keyvault"
mkdir -p "$BACKUP_DIR/storage"

echo "📋 Backup Configuration:"
echo "  Target Resource Group: $V1_RG"
echo "  Backup Directory: $BACKUP_DIR"
echo ""

# Safety checks
echo "🔍 Pre-backup Safety Checks..."

# Check if Azure CLI is available
if ! command -v az &> /dev/null; then
    echo "❌ Azure CLI not found. Please install Azure CLI first."
    exit 1
fi

# Check if logged in
if ! az account show &> /dev/null; then
    echo "❌ Not logged into Azure. Please run 'az login' first."
    exit 1
fi

# Check if Docker is available for container backup
if ! command -v docker &> /dev/null; then
    echo "⚠️  Docker not found. Container image backup will be limited to inventory only."
    DOCKER_AVAILABLE=false
else
    DOCKER_AVAILABLE=true
fi

echo "✅ Azure CLI available and authenticated"

# Check if resource group exists
if ! az group show --name "$V1_RG" &> /dev/null; then
    echo "✅ Resource group '$V1_RG' doesn't exist - nothing to backup"
    exit 0
fi

echo "📊 Found resource group '$V1_RG'"

# Container Registry Backup
echo ""
echo "📦 Container Registry Backup..."
REGISTRIES=$(az acr list -g "$V1_RG" --query "[].name" -o tsv 2>/dev/null || echo "")

if [ -n "$REGISTRIES" ]; then
    for registry in $REGISTRIES; do
        echo "  Backing up registry: $registry"
        
        # Save registry configuration
        az acr show --name "$registry" -g "$V1_RG" > "$BACKUP_DIR/container-images/${registry}-config.json"
        
        # List all repositories and tags
        echo "    Inventorying repositories..."
        az acr repository list --name "$registry" -o json > "$BACKUP_DIR/container-images/${registry}-repositories.json"
        
        REPOS=$(az acr repository list --name "$registry" -o tsv 2>/dev/null || echo "")
        if [ -n "$REPOS" ]; then
            echo "    Found repositories: $(echo $REPOS | wc -w)"
            
            # Create detailed backup plan
            echo "# Container Registry Backup Plan: $registry" > "$BACKUP_DIR/container-images/${registry}-backup-plan.md"
            echo "" >> "$BACKUP_DIR/container-images/${registry}-backup-plan.md"
            echo "## Registry Details" >> "$BACKUP_DIR/container-images/${registry}-backup-plan.md"
            echo "- Login Server: $(az acr show --name "$registry" -g "$V1_RG" --query "loginServer" -o tsv)" >> "$BACKUP_DIR/container-images/${registry}-backup-plan.md"
            echo "- SKU: $(az acr show --name "$registry" -g "$V1_RG" --query "sku.name" -o tsv)" >> "$BACKUP_DIR/container-images/${registry}-backup-plan.md"
            echo "" >> "$BACKUP_DIR/container-images/${registry}-backup-plan.md"
            echo "## Repositories and Tags" >> "$BACKUP_DIR/container-images/${registry}-backup-plan.md"
            
            for repo in $REPOS; do
                echo "      Repository: $repo"
                
                # Get tags for this repository
                TAGS=$(az acr repository show-tags --name "$registry" --repository "$repo" -o tsv 2>/dev/null || echo "")
                if [ -n "$TAGS" ]; then
                    echo "        Tags: $(echo $TAGS | tr '\n' ', ' | sed 's/,$//')"
                    
                    # Save to backup plan
                    echo "### $repo" >> "$BACKUP_DIR/container-images/${registry}-backup-plan.md"
                    for tag in $TAGS; do
                        echo "- \`$repo:$tag\`" >> "$BACKUP_DIR/container-images/${registry}-backup-plan.md"
                        
                        # If Docker is available, attempt to pull and save images
                        if [ "$DOCKER_AVAILABLE" = true ]; then
                            echo "        Attempting to backup image: $repo:$tag"
                            
                            # Login to ACR
                            if az acr login --name "$registry" &>/dev/null; then
                                FULL_IMAGE="${registry}.azurecr.io/$repo:$tag"
                                
                                # Pull image
                                if docker pull "$FULL_IMAGE" &>/dev/null; then
                                    # Save image to tar file
                                    IMAGE_FILE="$BACKUP_DIR/container-images/${registry}-${repo//\//-}-${tag}.tar"
                                    if docker save "$FULL_IMAGE" -o "$IMAGE_FILE"; then
                                        echo "          ✅ Image saved: $(basename "$IMAGE_FILE")"
                                        # Compress the tar file
                                        gzip "$IMAGE_FILE" 2>/dev/null || true
                                    else
                                        echo "          ⚠️  Failed to save image: $repo:$tag"
                                    fi
                                else
                                    echo "          ⚠️  Failed to pull image: $repo:$tag"
                                fi
                            else
                                echo "          ⚠️  Failed to login to registry for image backup"
                            fi
                        fi
                    done
                    echo "" >> "$BACKUP_DIR/container-images/${registry}-backup-plan.md"
                fi
            done
            
            # Add recovery instructions
            echo "## Recovery Instructions" >> "$BACKUP_DIR/container-images/${registry}-backup-plan.md"
            echo "" >> "$BACKUP_DIR/container-images/${registry}-backup-plan.md"
            echo "### To restore images:" >> "$BACKUP_DIR/container-images/${registry}-backup-plan.md"
            echo "\`\`\`bash" >> "$BACKUP_DIR/container-images/${registry}-backup-plan.md"
            echo "# Login to new registry" >> "$BACKUP_DIR/container-images/${registry}-backup-plan.md"
            echo "az acr login --name NEW_REGISTRY_NAME" >> "$BACKUP_DIR/container-images/${registry}-backup-plan.md"
            echo "" >> "$BACKUP_DIR/container-images/${registry}-backup-plan.md"
            echo "# Restore each image" >> "$BACKUP_DIR/container-images/${registry}-backup-plan.md"
            for repo in $REPOS; do
                TAGS=$(az acr repository show-tags --name "$registry" --repository "$repo" -o tsv 2>/dev/null || echo "")
                for tag in $TAGS; do
                    IMAGE_FILE="${registry}-${repo//\//-}-${tag}.tar.gz"
                    echo "gunzip -c $IMAGE_FILE | docker load" >> "$BACKUP_DIR/container-images/${registry}-backup-plan.md"
                    echo "docker tag ${registry}.azurecr.io/$repo:$tag NEW_REGISTRY.azurecr.io/$repo:$tag" >> "$BACKUP_DIR/container-images/${registry}-backup-plan.md"
                    echo "docker push NEW_REGISTRY.azurecr.io/$repo:$tag" >> "$BACKUP_DIR/container-images/${registry}-backup-plan.md"
                done
            done
            echo "\`\`\`" >> "$BACKUP_DIR/container-images/${registry}-backup-plan.md"
            
        else
            echo "    No repositories found in registry"
        fi
        echo "  ✅ Registry backup completed: $registry"
    done
else
    echo "  No container registries found"
fi

# SQL Database Backup
echo ""
echo "🗄️  SQL Database Backup..."
SQL_SERVERS=$(az sql server list -g "$V1_RG" --query "[].name" -o tsv 2>/dev/null || echo "")

if [ -n "$SQL_SERVERS" ]; then
    for server in $SQL_SERVERS; do
        echo "  Backing up SQL server: $server"
        
        # Save server configuration
        az sql server show --name "$server" -g "$V1_RG" > "$BACKUP_DIR/database/${server}-config.json"
        
        # List databases
        DATABASES=$(az sql db list --server "$server" -g "$V1_RG" --query "[?name != 'master'].name" -o tsv 2>/dev/null || echo "")
        if [ -n "$DATABASES" ]; then
            for db in $DATABASES; do
                echo "    Database: $db"
                
                # Save database configuration
                az sql db show --server "$server" --name "$db" -g "$V1_RG" > "$BACKUP_DIR/database/${server}-${db}-config.json"
                
                # Create backup export script
                cat > "$BACKUP_DIR/database/backup-${server}-${db}.sh" << EOF
#!/bin/bash
# SQL Database Backup Script for $server/$db

# Export database to bacpac
az sql db export \\
    --server "$server" \\
    --name "$db" \\
    --resource-group "$V1_RG" \\
    --storage-uri "https://STORAGE_ACCOUNT.blob.core.windows.net/backups/${db}-\$(date +%Y%m%d-%H%M%S).bacpac" \\
    --storage-key "STORAGE_ACCOUNT_KEY" \\
    --admin-user "SQL_ADMIN_USER" \\
    --admin-password "SQL_ADMIN_PASSWORD"

echo "✅ Database exported: $db"
EOF
                chmod +x "$BACKUP_DIR/database/backup-${server}-${db}.sh"
                echo "      ✅ Backup script created: backup-${server}-${db}.sh"
            done
        else
            echo "    No user databases found"
        fi
    done
else
    echo "  No SQL servers found"
fi

# Key Vault Backup
echo ""
echo "🔐 Key Vault Backup..."
KEY_VAULTS=$(az keyvault list -g "$V1_RG" --query "[].name" -o tsv 2>/dev/null || echo "")

if [ -n "$KEY_VAULTS" ]; then
    for vault in $KEY_VAULTS; do
        echo "  Backing up Key Vault: $vault"
        
        # Save vault configuration
        az keyvault show --name "$vault" -g "$V1_RG" > "$BACKUP_DIR/keyvault/${vault}-config.json"
        
        # Create secrets backup script (requires proper permissions)
        cat > "$BACKUP_DIR/keyvault/backup-${vault}-secrets.sh" << EOF
#!/bin/bash
# Key Vault Secrets Backup Script for $vault

mkdir -p keyvault-secrets-backup/$vault

# List all secrets
SECRETS=\$(az keyvault secret list --vault-name "$vault" --query "[].name" -o tsv 2>/dev/null || echo "")

if [ -n "\$SECRETS" ]; then
    for secret in \$SECRETS; do
        echo "  Backing up secret: \$secret"
        
        # Get secret value (requires Key Vault Secrets User role)
        SECRET_VALUE=\$(az keyvault secret show --vault-name "$vault" --name "\$secret" --query "value" -o tsv 2>/dev/null)
        
        if [ -n "\$SECRET_VALUE" ]; then
            echo "\$SECRET_VALUE" > "keyvault-secrets-backup/$vault/\${secret}.txt"
            echo "    ✅ Secret backed up: \$secret"
        else
            echo "    ⚠️  Could not access secret: \$secret (check permissions)"
        fi
    done
else
    echo "  No secrets found or no access"
fi

echo "✅ Key Vault backup completed: $vault"
EOF
        chmod +x "$BACKUP_DIR/keyvault/backup-${vault}-secrets.sh"
        echo "  ✅ Backup script created: backup-${vault}-secrets.sh"
        
        # Create restore script
        cat > "$BACKUP_DIR/keyvault/restore-${vault}-secrets.sh" << EOF
#!/bin/bash
# Key Vault Secrets Restore Script for $vault

NEW_VAULT_NAME="\$1"

if [ -z "\$NEW_VAULT_NAME" ]; then
    echo "Usage: \$0 <new-vault-name>"
    exit 1
fi

# Restore all secrets from backup
for secret_file in keyvault-secrets-backup/$vault/*.txt; do
    if [ -f "\$secret_file" ]; then
        SECRET_NAME=\$(basename "\$secret_file" .txt)
        SECRET_VALUE=\$(cat "\$secret_file")
        
        echo "Restoring secret: \$SECRET_NAME"
        az keyvault secret set --vault-name "\$NEW_VAULT_NAME" --name "\$SECRET_NAME" --value "\$SECRET_VALUE"
    fi
done

echo "✅ All secrets restored to: \$NEW_VAULT_NAME"
EOF
        chmod +x "$BACKUP_DIR/keyvault/restore-${vault}-secrets.sh"
    done
else
    echo "  No key vaults found"
fi

# Storage Account Backup
echo ""
echo "🗄️  Storage Account Backup..."
STORAGE_ACCOUNTS=$(az storage account list -g "$V1_RG" --query "[].name" -o tsv 2>/dev/null || echo "")

if [ -n "$STORAGE_ACCOUNTS" ]; then
    for account in $STORAGE_ACCOUNTS; do
        echo "  Backing up Storage Account: $account"
        
        # Save storage account configuration
        az storage account show --name "$account" -g "$V1_RG" > "$BACKUP_DIR/storage/${account}-config.json"
        
        # Create blob backup script
        cat > "$BACKUP_DIR/storage/backup-${account}-blobs.sh" << EOF
#!/bin/bash
# Storage Account Blob Backup Script for $account

mkdir -p storage-backup/$account

# Get storage account key
STORAGE_KEY=\$(az storage account keys list --account-name "$account" -g "$V1_RG" --query "[0].value" -o tsv)

# List all containers
CONTAINERS=\$(az storage container list --account-name "$account" --account-key "\$STORAGE_KEY" --query "[].name" -o tsv 2>/dev/null || echo "")

if [ -n "\$CONTAINERS" ]; then
    for container in \$CONTAINERS; do
        echo "  Backing up container: \$container"
        mkdir -p "storage-backup/$account/\$container"
        
        # Download all blobs in container
        az storage blob download-batch \\
            --destination "storage-backup/$account/\$container" \\
            --source "\$container" \\
            --account-name "$account" \\
            --account-key "\$STORAGE_KEY"
        
        echo "    ✅ Container backed up: \$container"
    done
else
    echo "  No containers found"
fi

echo "✅ Storage account backup completed: $account"
EOF
        chmod +x "$BACKUP_DIR/storage/backup-${account}-blobs.sh"
        echo "  ✅ Backup script created: backup-${account}-blobs.sh"
    done
else
    echo "  No storage accounts found"
fi

# Create master backup execution script
echo ""
echo "📋 Creating Master Backup Script..."
cat > "$BACKUP_DIR/execute-all-backups.sh" << EOF
#!/bin/bash
# Master Backup Execution Script
# Executes all backup scripts in sequence

echo "🚀 Executing All V1 Resource Backups..."
echo "======================================"

# Execute all backup scripts
for script in \$(find . -name "backup-*.sh" | sort); do
    echo ""
    echo "📦 Executing: \$script"
    chmod +x "\$script"
    if ./"\$script"; then
        echo "✅ Completed: \$script"
    else
        echo "❌ Failed: \$script"
    fi
done

echo ""
echo "✅ All backup scripts executed"
echo "📞 Review results and check for any failures"
EOF
chmod +x "$BACKUP_DIR/execute-all-backups.sh"

echo "✅ Backup preparation completed successfully"
echo ""
echo "📋 Backup Summary:"
echo "  • Backup directory: $BACKUP_DIR"
echo "  • Container registry backup plans created"
echo "  • Database backup scripts created"
echo "  • Key Vault backup scripts created"
echo "  • Storage account backup scripts created"
echo "  • Master execution script: execute-all-backups.sh"
echo ""
echo "🚀 Next Steps:"
echo "  1. Review backup plans in: $BACKUP_DIR"
echo "  2. Execute specific backup scripts as needed"
echo "  3. Run master script: $BACKUP_DIR/execute-all-backups.sh"
echo "  4. Verify backups before proceeding with cleanup"
echo ""
echo "⚠️  Important: Run backups before any resource deletion!"
#!/bin/bash

echo "🚨 EMERGENCY STYLES POPULATION"
echo "==============================="
echo ""

# Configuration
RESOURCE_GROUP="rg-aiprofilemaker-staging"
SERVER_NAME="aiprofilemaker-sql-staging"
DATABASE_NAME="aiprofilemakerdb"

echo "📋 Database Details:"
echo "  Resource Group: $RESOURCE_GROUP"
echo "  Server: $SERVER_NAME"
echo "  Database: $DATABASE_NAME"
echo ""

# Check Azure login
echo "🔐 Checking Azure authentication..."
if ! az account show &>/dev/null; then
    echo "❌ Not logged into Azure. Run: az login"
    exit 1
fi

ACCOUNT_NAME=$(az account show --query user.name -o tsv)
echo "✅ Logged into Azure as: $ACCOUNT_NAME"
echo ""

# Create SQL script
cat > /tmp/emergency-styles.sql << 'EOF'
-- Emergency styles population
DELETE FROM Styles;

INSERT INTO Styles (Name, Category, Description, IsActive) VALUES
('professional', 'Business', 'Professional business headshot style', 1),
('casual', 'Lifestyle', 'Casual everyday portrait style', 1),
('artistic', 'Creative', 'Artistic and creative portrait style', 1),
('corporate', 'Business', 'Corporate executive professional style', 1),
('executive', 'Business', 'Senior executive leadership style', 1),
('consultant', 'Business', 'Professional consultant style', 1),
('linkedin', 'Business', 'LinkedIn profile optimized style', 1),
('legal', 'Business', 'Legal professional style', 1),
('medical', 'Professional', 'Healthcare professional style', 1),
('academic', 'Professional', 'Academic and research style', 1),
('entrepreneur', 'Business', 'Entrepreneur and startup style', 1),
('startup', 'Business', 'Startup founder style', 1),
('tech-professional', 'Technology', 'Technology industry style', 1),
('influencer', 'Social', 'Social media influencer style', 1),
('digital-nomad', 'Lifestyle', 'Remote work professional style', 1),
('creative', 'Creative', 'Creative industry professional style', 1),
('edgy-urban', 'Creative', 'Modern urban creative style', 1),
('glamour', 'Lifestyle', 'Glamorous portrait style', 1),
('fitness', 'Lifestyle', 'Health and fitness professional style', 1),
('spiritual', 'Lifestyle', 'Wellness and spiritual style', 1),
('author', 'Creative', 'Literary author style', 1);

SELECT COUNT(*) as total_styles FROM Styles WHERE IsActive = 1;
EOF

echo "🔄 Executing SQL against Azure database..."
echo ""

# Execute SQL
az sql db query \
    --resource-group "$RESOURCE_GROUP" \
    --server "$SERVER_NAME" \
    --database "$DATABASE_NAME" \
    --queries @/tmp/emergency-styles.sql \
    --output table

if [ $? -eq 0 ]; then
    echo ""
    echo "🎉 SUCCESS: Emergency styles population completed!"
    echo ""
    echo "🧪 Verification:"
    
    # Test API immediately
    echo "Testing API..."
    API_RESPONSE=$(curl -s "https://aiprofilemaker-api-staging.thankfulriver-68674ea3.eastus2.azurecontainerapps.io/api/style")
    STYLE_COUNT=$(echo "$API_RESPONSE" | python3 -c "
import sys, json
try:
    data = json.load(sys.stdin)
    print(len(data.get('data', [])))
except:
    print('0')
")
    
    if [ "$STYLE_COUNT" -ge 20 ]; then
        echo "✅ SUCCESS: API returns $STYLE_COUNT styles"
        echo "✅ Database population successful!"
    elif [ "$STYLE_COUNT" -eq 0 ]; then
        echo "❌ FAILED: API still returns 0 styles"
        echo "❌ Database population may have failed"
    else
        echo "⚠️  PARTIAL: API returns $STYLE_COUNT styles (expected 21)"
    fi
    
else
    echo "❌ ERROR: Failed to execute SQL"
    exit 1
fi

# Clean up
rm -f /tmp/emergency-styles.sql

echo ""
echo "✅ Emergency fix completed!"
#!/bin/bash

# Style Preview Case Sensitivity Fix
# This script copies lowercase style preview images to their capitalized versions
# to handle frontend requests that may use different case formats

set -e

# Define mappings from lowercase to expected frontend case
declare -A style_mappings=(
    ["academic"]="Academic"
    ["artistic"]="Artistic"
    ["author"]="Author"
    ["casual"]="Casual"
    ["consultant"]="Consultant"
    ["corporate"]="Corporate"
    ["creative"]="Creative"
    ["digital-nomad"]="Digital-Nomad"
    ["edgy-urban"]="Edgy-Urban"
    ["entrepreneur"]="Entrepreneur"
    ["executive"]="Executive"
    ["fashion"]="Fashion"
    ["fitness"]="Fitness"
    ["glamour"]="Glamour"
    ["influencer"]="Influencer"
    ["legal"]="Legal"
    ["linkedin"]="LinkedIn"
    ["medical"]="Medical"
    ["professional"]="Professional"
    ["spiritual"]="Spiritual"
    ["startup"]="Startup"
    ["tech-professional"]="Tech-Professional"
)

# Azure storage account
STORAGE_ACCOUNT="aipmstv16j74jubocuukg"
CONTAINER="style-previews"

echo "🔧 Fixing style preview case sensitivity issues..."
echo "📋 Processing ${#style_mappings[@]} style mappings"

success_count=0
error_count=0

for lowercase in "${!style_mappings[@]}"; do
    capitalized="${style_mappings[$lowercase]}"
    
    echo -n "📋 $lowercase.jpg → $capitalized.jpg: "
    
    # Check if source file exists
    source_status=$(curl -s -o /dev/null -w "%{http_code}" "https://${STORAGE_ACCOUNT}.blob.core.windows.net/${CONTAINER}/${lowercase}.jpg")
    
    if [ "$source_status" != "200" ]; then
        echo "❌ Source file not found"
        ((error_count++))
        continue
    fi
    
    # Check if destination already exists
    dest_status=$(curl -s -o /dev/null -w "%{http_code}" "https://${STORAGE_ACCOUNT}.blob.core.windows.net/${CONTAINER}/${capitalized}.jpg")
    
    if [ "$dest_status" == "200" ]; then
        echo "✅ Already exists"
        ((success_count++))
        continue
    fi
    
    # Copy the file
    result=$(az storage blob copy start \
        --source-container "$CONTAINER" \
        --source-blob "${lowercase}.jpg" \
        --destination-container "$CONTAINER" \
        --destination-blob "${capitalized}.jpg" \
        --account-name "$STORAGE_ACCOUNT" \
        --output json 2>/dev/null)
    
    if [ $? -eq 0 ]; then
        copy_status=$(echo "$result" | grep -o '"copy_status": "[^"]*"' | cut -d'"' -f4)
        if [ "$copy_status" == "success" ]; then
            echo "✅ Copied"
            ((success_count++))
        else
            echo "❌ Copy failed: $copy_status"
            ((error_count++))
        fi
    else
        echo "❌ Copy command failed"
        ((error_count++))
    fi
done

echo ""
echo "📊 Summary:"
echo "  ✅ Success: $success_count"
echo "  ❌ Errors: $error_count"
echo "  📋 Total: ${#style_mappings[@]}"

if [ $error_count -eq 0 ]; then
    echo "🎉 All style preview case sensitivity issues have been resolved!"
else
    echo "⚠️  Some issues remain. Check the errors above."
fi
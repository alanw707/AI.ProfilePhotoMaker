#!/bin/bash

# Cleanup orphaned database records using API endpoints
# This will remove database records that point to non-existent files

echo "Running orphaned records cleanup..."

# Option 1: Cleanup orphaned records (removes DB records with missing files)
curl -X POST "https://localhost:5035/api/image/debug/cleanup-orphaned-records" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN_HERE"

echo -e "\nDone! Check the response to see what was cleaned up."

# Option 2: If you want to run the complete repair process
echo -e "\nAlternatively, run complete repair:"
echo "curl -X POST 'https://localhost:5035/api/image/debug/complete-repair' -H 'Authorization: Bearer YOUR_TOKEN_HERE'"
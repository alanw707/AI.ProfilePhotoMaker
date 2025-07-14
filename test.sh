#!/bin/bash
# Test script for local development
# Usage: ./test.sh [--watch] [--coverage] [--filter <pattern>]

set -e

WATCH_MODE=false
COVERAGE=false
FILTER_PATTERN=""

# Parse command line arguments
while [[ $# -gt 0 ]]; do
  case $1 in
    --watch)
      WATCH_MODE=true
      shift
      ;;
    --coverage)
      COVERAGE=true
      shift
      ;;
    --filter)
      FILTER_PATTERN="$2"
      shift 2
      ;;
    *)
      echo "Unknown argument: $1"
      echo "Usage: $0 [--watch] [--coverage] [--filter <pattern>]"
      exit 1
      ;;
  esac
done

echo "🧪 AI.ProfilePhotoMaker Test Runner"
echo "=================================="

# Build the solution first
echo "🔨 Building solution..."
dotnet build --configuration Release

if [ "$WATCH_MODE" = true ]; then
  echo "👀 Running tests in watch mode..."
  if [ -n "$FILTER_PATTERN" ]; then
    dotnet watch test --filter "$FILTER_PATTERN"
  else
    dotnet watch test
  fi
elif [ "$COVERAGE" = true ]; then
  echo "📊 Running tests with coverage..."
  TEST_CMD="dotnet test --configuration Release --collect:\"XPlat Code Coverage\" --logger trx --results-directory TestResults"
  if [ -n "$FILTER_PATTERN" ]; then
    TEST_CMD="$TEST_CMD --filter \"$FILTER_PATTERN\""
  fi
  eval $TEST_CMD
  
  echo "📈 Coverage reports generated in TestResults/"
else
  echo "🚀 Running tests..."
  TEST_CMD="dotnet test --configuration Release --logger trx --results-directory TestResults"
  if [ -n "$FILTER_PATTERN" ]; then
    TEST_CMD="$TEST_CMD --filter \"$FILTER_PATTERN\""
  fi
  eval $TEST_CMD
fi

echo "✅ Tests completed!"
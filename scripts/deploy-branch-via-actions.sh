#!/bin/bash

# Automated branch deployment helper
# Builds/pushes Docker images, pushes the target branch, and
# triggers the "Simple Deploy" workflow while streaming logs.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$PROJECT_ROOT"

WORKFLOW_FILE="simple-deploy.yml"
WORKFLOW_NAME="🚀 Simple Deploy (Local Build to MVP environment)"

BLUE='\033[0;34m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
BOLD='\033[1m'
NC='\033[0m'

BRANCH=""
SKIP_TESTS="false"
RUN_DB_MIGRATIONS="true"
AUTO_PUSH="true"
IMAGE_TAG="${IMAGE_TAG:-}"
BUILD_NUMBER=""

usage() {
    cat <<EOF2
Usage: scripts/deploy-branch-via-actions.sh [options]

Options:
  --branch <name>      Branch to deploy (defaults to current)
  --skip-tests         Pass skip_tests=true to workflow
  --skip-db-migrations Pass run_db_migrations=false to workflow
  --run-db-migrations  Pass run_db_migrations=true to workflow
  --no-git-push        Do not push the branch before triggering workflow
  --image-tag <tag>    Override IMAGE_TAG/BUILD_NUMBER (default timestamp)
  --help               Show this help

Prereqs: docker, az cli (for push script), gh cli, jq, authenticated Azure and GitHub sessions.
EOF2
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --branch)
            BRANCH="$2"; shift 2;;
        --skip-tests)
            SKIP_TESTS="true"; shift;;
        --skip-db-migrations)
            RUN_DB_MIGRATIONS="false"; shift;;
        --run-db-migrations)
            RUN_DB_MIGRATIONS="true"; shift;;
        --no-git-push)
            AUTO_PUSH="false"; shift;;
        --image-tag)
            IMAGE_TAG="$2"; shift 2;;
        --help)
            usage; exit 0;;
        *)
            echo -e "${RED}Unknown option: $1${NC}" >&2
            usage; exit 1;;
    esac
done

if [[ -z "$BRANCH" ]]; then
    BRANCH="$(git rev-parse --abbrev-ref HEAD)"
fi

if [[ -z "$IMAGE_TAG" ]]; then
    IMAGE_TAG="$(date +%Y%m%d-%H%M%S)"
fi
BUILD_NUMBER="$IMAGE_TAG"

HEAD_SHA="$(git rev-parse "$BRANCH")"

echo -e "${BOLD}${BLUE}🚀 Branch deployment helper${NC}"
echo -e "${BLUE}Branch:${NC} $BRANCH"
echo -e "${BLUE}Image Tag:${NC} $IMAGE_TAG"
echo -e "${BLUE}Skip Tests:${NC} $SKIP_TESTS"
echo -e "${BLUE}Run DB Migrations:${NC} $RUN_DB_MIGRATIONS"
echo

for tool in gh jq git; do
    if ! command -v "$tool" >/dev/null 2>&1; then
        echo -e "${RED}Missing required tool: $tool${NC}" >&2
        exit 1
    fi
done

if ! gh auth status >/dev/null 2>&1; then
    echo -e "${RED}GitHub CLI not authenticated. Run 'gh auth login'.${NC}" >&2
    exit 1
fi

echo -e "${BLUE}🏗️ Building Docker images...${NC}"
IMAGE_TAG="$IMAGE_TAG" BUILD_NUMBER="$BUILD_NUMBER" ./scripts/build-local.sh

echo -e "${BLUE}📦 Pushing Docker images to ACR...${NC}"
IMAGE_TAG="$IMAGE_TAG" BUILD_NUMBER="$BUILD_NUMBER" ./scripts/push-to-acr.sh

if [[ "$AUTO_PUSH" == "true" ]]; then
    echo -e "${BLUE}⬆️ Ensuring branch '$BRANCH' is pushed...${NC}"
    if git rev-parse --abbrev-ref --symbolic-full-name "$BRANCH@{u}" >/dev/null 2>&1; then
        git push origin "$BRANCH"
    else
        git push -u origin "$BRANCH"
    fi
else
    echo -e "${YELLOW}Skipping git push per flag. Ensure remote branch is updated manually.${NC}"
fi

START_EPOCH=$(date -u +%s)

echo -e "${BLUE}▶️ Triggering workflow '${WORKFLOW_NAME}' on branch ${BRANCH}...${NC}"
gh workflow run "$WORKFLOW_FILE" --ref "$BRANCH" -f skip_tests="$SKIP_TESTS" -f run_db_migrations="$RUN_DB_MIGRATIONS"

echo -e "${BLUE}⏱️ Waiting for workflow run to register...${NC}"
RUN_ID=""
for attempt in {1..30}; do
    RUN_JSON=$(gh run list --workflow "$WORKFLOW_FILE" --branch "$BRANCH" --event workflow_dispatch --limit 10 --json databaseId,headSha,headBranch,createdAt,status 2>/dev/null || echo "[]")
    RUN_ID=$(echo "$RUN_JSON" | jq -r --arg sha "$HEAD_SHA" --arg branch "$BRANCH" --argjson start "$START_EPOCH" '
        map(select(.headBranch == $branch and .headSha == $sha and ((.createdAt | fromdateiso8601) >= $start)))
        | sort_by(.createdAt)
        | last
        | .databaseId // empty')
    if [[ -n "$RUN_ID" ]]; then
        break
    fi
    sleep 5
done

if [[ -z "$RUN_ID" ]]; then
    echo -e "${YELLOW}⚠️ Could not determine workflow run ID automatically.${NC}"
    echo "Manually monitor: gh run list --workflow $WORKFLOW_FILE --branch $BRANCH"
    exit 0
fi

echo -e "${BLUE}📡 Monitoring run $RUN_ID...${NC}"
if gh run watch "$RUN_ID"; then
    CONCLUSION=$(gh run view "$RUN_ID" --json conclusion --jq '.conclusion')
    if [[ "$CONCLUSION" == "success" ]]; then
        echo -e "${GREEN}✅ Deployment succeeded.${NC}"
    else
        echo -e "${RED}❌ Workflow finished with conclusion: $CONCLUSION${NC}"
        exit 1
    fi
else
    echo -e "${RED}Failed to stream workflow logs. View run at:${NC} https://github.com/$(gh repo view --json nameWithOwner -q '.nameWithOwner')/actions/runs/$RUN_ID"
    exit 1
fi

echo -e "${GREEN}🎉 Branch deployment complete.${NC}"

#!/usr/bin/env bash
# setup-branch-protection.sh
#
# Configures branch protection rules on the main branch.
# Run once after repository creation and after at least one commit to main.
#
# Prerequisites:
#   - gh CLI installed and authenticated (gh auth login)
#   - GITHUB_REPO set to owner/repo
#   - CI workflow name must match REQUIRED_STATUS_CHECK below
#
# Usage:
#   GITHUB_REPO=owner/repo bash setup/setup-branch-protection.sh

set -euo pipefail

REPO="${GITHUB_REPO:?Set GITHUB_REPO=owner/repo before running}"
REQUIRED_STATUS_CHECK="build-and-test"   # Must match the job name in ci.yml

echo "Configuring branch protection for $REPO main branch..."

gh api \
  --method PUT \
  "repos/$REPO/branches/main/protection" \
  --field "required_status_checks[strict]=true" \
  --field "required_status_checks[contexts][]=$REQUIRED_STATUS_CHECK" \
  --field "enforce_admins=false" \
  --field "required_pull_request_reviews=null" \
  --field "restrictions=null" \
  --field "allow_force_pushes=false" \
  --field "allow_deletions=false"

echo "Branch protection applied:"
echo "  - Direct pushes to main: BLOCKED"
echo "  - Required CI check: $REQUIRED_STATUS_CHECK"
echo "  - Force pushes: BLOCKED"
echo "  - Branch deletion: BLOCKED"
echo ""
echo "Note: PR merge is controlled by the merge-gate workflow + PM agent."
echo "No GitHub-native reviewer requirement is set (label-based gate is used instead)."

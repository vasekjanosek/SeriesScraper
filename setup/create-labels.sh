#!/usr/bin/env bash
# create-labels.sh
#
# Creates all GitHub Issue labels required for the agent state machine.
# Run once after repository creation.
#
# Prerequisites:
#   - gh CLI installed and authenticated (gh auth login)
#   - GITHUB_REPO set to owner/repo (e.g. vasekjanosek/SeriesScraper)
#
# Usage:
#   GITHUB_REPO=owner/repo bash setup/create-labels.sh

set -euo pipefail

REPO="${GITHUB_REPO:?Set GITHUB_REPO=owner/repo before running}"

echo "Creating labels for $REPO..."

create_label() {
  local name="$1"
  local color="$2"
  local description="$3"

  if gh label list --repo "$REPO" --json name --jq '.[].name' | grep -qx "$name"; then
    echo "  [skip] $name (already exists)"
  else
    gh label create "$name" \
      --repo "$REPO" \
      --color "$color" \
      --description "$description"
    echo "  [created] $name"
  fi
}

# ─── Status Labels ────────────────────────────────────────────────────────────
echo ""
echo "Status labels..."
create_label "status:backlog"      "ededed" "Created, not yet planned"
create_label "status:ready"        "0075ca" "Planned, ready for agent pickup"
create_label "status:in-progress"  "fbca04" "Agent actively working"
create_label "status:in-review"    "e4e669" "Under Reviewer agent"
create_label "status:in-testing"       "f9d0c4" "Under Tester agent"
create_label "status:security-review" "d93f0b" "Under Security agent code review"
create_label "status:awaiting-pm"     "d93f0b" "Waiting for PM acceptance review"
create_label "status:pm-approved"  "0e8a16" "PM approved — merge triggered"
create_label "status:done"         "006b75" "Merged and closed"
create_label "status:blocked"      "b60205" "Waiting on dependency"
create_label "status:on-hold"      "cccccc" "Paused by user decision"

# ─── Agent Assignment Labels ──────────────────────────────────────────────────
echo ""
echo "Agent labels..."
create_label "agent:orchestrator"   "1d76db" "Owned by Orchestrator"
create_label "agent:pm"             "0052cc" "Owned by Product Manager"
create_label "agent:architect"      "5319e7" "Owned by Architect"
create_label "agent:planner"        "006b75" "Owned by Planner"
create_label "agent:developer"      "0075ca" "Owned by Developer"
create_label "agent:reviewer"       "e4e669" "Owned by Reviewer"
create_label "agent:tester"         "f9d0c4" "Owned by Tester"
create_label "agent:ux"             "fef2c0" "Owned by UX Designer"
create_label "agent:devops"         "ededed" "Owned by DevOps"
create_label "agent:security"       "b60205" "Owned by Security/Pentester"
create_label "agent:data-engineer"  "006b75" "Owned by Data Engineer"
create_label "agent:research"       "bfd4f2" "Owned by Research agent"
create_label "agent:evaluator"      "d4c5f9" "Owned by Evaluation agent"

# ─── Developer Slot Labels ───────────────────────────────────────────────────
echo ""
echo "Developer slot labels..."
create_label "dev-slot:1"  "c2e0c6" "Developer slot 1"
create_label "dev-slot:2"  "c2e0c6" "Developer slot 2"
create_label "dev-slot:3"  "c2e0c6" "Developer slot 3"

# ─── Type Labels ─────────────────────────────────────────────────────────────
echo ""
echo "Type labels..."
create_label "type:epic"            "3e4b9e" "Epic — parent of features"
create_label "type:feature"         "0075ca" "New feature"
create_label "type:bug"             "d73a4a" "Something is broken"
create_label "type:task"            "ededed" "Development task"
create_label "type:research"        "bfd4f2" "Research task"
create_label "type:design"          "fef2c0" "UI/UX design task"
create_label "type:infrastructure"  "006b75" "Infrastructure / DevOps task"
create_label "type:security"        "b60205" "Security finding or task"

# ─── Priority Labels ─────────────────────────────────────────────────────────
echo ""
echo "Priority labels..."
create_label "priority:critical"  "b60205" "Blocks other work — must fix immediately"
create_label "priority:high"      "d93f0b" "Important, do next"
create_label "priority:medium"    "fbca04" "Normal priority"
create_label "priority:low"       "e4e669" "Nice to have"

# ─── Gate Labels ─────────────────────────────────────────────────────────────
echo ""
echo "Gate labels..."
create_label "gate:scope"            "d4c5f9" "PM scope definition phase active"
create_label "gate:research"         "bfd4f2" "Research phase active — blocks architecture"
create_label "gate:architecture"     "5319e7" "Architecture phase active"
create_label "gate:security-review"  "b60205" "Security design review phase active"
create_label "gate:planning"         "1d76db" "Planning phase active"

# ─── Special Labels ──────────────────────────────────────────────────────────
echo ""
echo "Special labels..."
create_label "needs-human"     "b60205" "Requires user intervention"
create_label "conflict-loop"   "d93f0b" "Stuck in conflict cycle loop"

echo ""
echo "Done. All labels created for $REPO."
